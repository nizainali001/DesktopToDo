using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Forms;
using System.Windows.Interop;
using Microsoft.Win32;
using DesktopToDo.Models;
using DesktopToDo.Services;

namespace DesktopToDo
{
    public partial class MainWindow : Window
    {
        private List<TaskItem> _allTasks = new List<TaskItem>();
        private AppSettings _settings = new AppSettings();
        private ReminderService _reminderService = new ReminderService();
        private NotifyIcon? _notifyIcon;
        private System.Windows.Threading.DispatcherTimer? _windowStateTimer;
        private bool _isShowingModal;
        private bool _isUserHidden;
        private int _repeatCheckCounter;
        private bool _isCompletedVisible = false;
        private TaskDisplayItem? _selectedTask;
        private const int EdgeHideThreshold = 5;

        public ObservableCollection<TaskDisplayItem> PendingTasks { get; set; } = new ObservableCollection<TaskDisplayItem>();
        public ObservableCollection<TaskDisplayItem> CompletedTasks { get; set; } = new ObservableCollection<TaskDisplayItem>();

        public MainWindow()
        {
            InitializeComponent();
            LoadData();
            RestoreRepeatingTasks();
            SetupReminder();
            SetupNotifyIcon();
            ApplySettings();
            BindData();
            SetupDragMove();
            SetupWindowStateTimer();
            SetupEdgeHide();
            SetupContextMenu();
            this.SourceInitialized += OnSourceInitialized;
            this.Activated += (s, e) => EnsureWindowCorrectPosition();
            this.Deactivated += (s, e) => { /* 可用于边缘隐藏检测 */ };
            
            // 确保窗口可见
            this.Show();
            this.Activate();
            this.Visibility = Visibility.Visible;
            this.WindowState = WindowState.Normal;
        }

        private void SetupWindowStateTimer()
        {
            _windowStateTimer = new System.Windows.Threading.DispatcherTimer();
            _windowStateTimer.Interval = TimeSpan.FromMilliseconds(50);
            _windowStateTimer.Tick += OnWindowStateTimerTick;
            _windowStateTimer.Start();

            // 监听窗口大小变化
            this.SizeChanged += OnWindowSizeChanged;
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 当宽度小于350且已完成列表展开时，自动收起
            if (this.Width < 350 && _isCompletedVisible)
            {
                ToggleCompletedPanel(false);
            }
        }

        private void ToggleCompletedPanel(bool show)
        {
            _isCompletedVisible = show;
            if (_isCompletedVisible)
            {
                CompletedColumn.Width = new GridLength(1, GridUnitType.Star);
                CompletedPanel.Visibility = Visibility.Visible;
                ToggleText.Text = "«";
                if (this.Width < 350)
                {
                    this.Width = 500; // 自动调整宽度
                }
            }
            else
            {
                CompletedColumn.Width = new GridLength(0);
                CompletedPanel.Visibility = Visibility.Collapsed;
                ToggleText.Text = "»";
            }
        }

        private void OnWindowStateTimerTick(object? sender, EventArgs e)
        {
            if (_isShowingModal)
                return;

            // 壁纸模式下不再每50ms调用SetWindowPos，改为仅在需要时调用
            // 避免桌面闪烁

            _repeatCheckCounter++;
            if (_repeatCheckCounter >= 1000)
            {
                _repeatCheckCounter = 0;
                RestoreRepeatingTasks();
            }

            // 边缘自动隐藏检测
            CheckEdgeHide();
        }

        #region 边缘自动隐藏

        private void SetupEdgeHide()
        {
            // 快捷键注册在 SourceInitialized 后
        }

        private HwndSource? _hwndSource;

        private void RegisterHotkey()
        {
            _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _hwndSource?.AddHook(WndProc);

            if (_settings.IsEdgeHideEnabled)
            {
                // 注册 Ctrl+H 快捷键
                RegisterGlobalHotKey();
            }
        }

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleHideWindow();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void RegisterGlobalHotKey()
        {
            // Ctrl+H = MOD_CONTROL | VK_H
            const int MOD_CONTROL = 0x0002;
            const int VK_H = 0x48;
            Win32Helper.RegisterHotKey(new WindowInteropHelper(this).Handle, HOTKEY_ID, MOD_CONTROL, VK_H);
        }

        private void UnregisterGlobalHotKey()
        {
            Win32Helper.UnregisterHotKey(new WindowInteropHelper(this).Handle, HOTKEY_ID);
        }

        private bool _isEdgeHidden = false;
        private bool _isKeyHidden = false; // 快捷键隐藏状态
        private double _originalLeft;
        private double _originalTop;
        private double _originalWidth;
        private double _originalHeight;
        private DateTime _lastEdgeActionTime = DateTime.MinValue;
        // 动态冷却时间（毫秒）：根据鼠标距离边缘的远近调整
        // 鼠标在边缘时冷却时间短，远离时冷却时间长
        private int GetEdgeActionCooldownMs(System.Drawing.Point mousePos)
        {
            var screen = SystemParameters.WorkArea;
            double distance = 0;
            
            // 计算鼠标到最近边缘的距离
            if (_isEdgeHidden)
            {
                // 窗口已隐藏，计算到对应隐藏边缘的距离
                switch (_edgeHideSide)
                {
                    case "left":
                        distance = mousePos.X;
                        break;
                    case "right":
                        distance = screen.Width - mousePos.X;
                        break;
                    case "top":
                        distance = mousePos.Y;
                        break;
                }
            }
            else
            {
                // 窗口未隐藏，计算到任意边缘的最小距离
                double distLeft = mousePos.X;
                double distRight = screen.Width - mousePos.X;
                double distTop = mousePos.Y;
                double distBottom = screen.Height - mousePos.Y;
                distance = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));
            }
            
            // 距离越小，冷却时间越短（最低500ms，最高3000ms）
            // 距离 < 50px：500ms
            // 距离 50-200px：500-3000ms
            // 距离 > 200px：3000ms
            if (distance < 50)
                return 500;
            else if (distance > 200)
                return 3000;
            else
                return (int)(500 + (distance - 50) * (2500.0 / 150.0));
        }
        private System.Drawing.Point? _mousePosWhenHidden = null;
        private DateTime _lastMouseNearWindowTime = DateTime.MinValue; // 记录鼠标靠近窗口的时间
        private const int HideDelayAfterMouseLeaveMs = 1000; // 鼠标离开后延迟1秒隐藏

        private void CheckEdgeHide()
        {
            if (!_settings.IsEdgeHideEnabled || _isUserHidden || _isShowingModal || _isKeyHidden)
                return;

            var now = DateTime.Now;
            var mousePos = System.Windows.Forms.Cursor.Position;
            var screen = SystemParameters.WorkArea;
            
            // 使用动态冷却时间
            var cooldownMs = GetEdgeActionCooldownMs(mousePos);
            
            if (_isEdgeHidden)
            {
                // 计算鼠标移动距离，防止轻微抖动触发显示
                if (_mousePosWhenHidden.HasValue)
                {
                    var dx = Math.Abs(mousePos.X - _mousePosWhenHidden.Value.X);
                    var dy = Math.Abs(mousePos.Y - _mousePosWhenHidden.Value.Y);
                    if (dx < 5 && dy < 5)
                        return; // 鼠标几乎没动，不触发
                }

                // 检查鼠标是否靠近隐藏的边缘区域
                bool shouldShow = false;
                int triggerDistance = 15; // 触发显示的距离

                switch (_edgeHideSide)
                {
                    case "left":
                        if (mousePos.X <= triggerDistance)
                            shouldShow = true;
                        break;
                    case "right":
                        if (mousePos.X >= screen.Width - triggerDistance)
                            shouldShow = true;
                        break;
                    case "top":
                        if (mousePos.Y <= triggerDistance)
                            shouldShow = true;
                        break;
                }

                if (shouldShow && (now - _lastEdgeActionTime).TotalMilliseconds >= cooldownMs)
                {
                    _lastEdgeActionTime = now;
                    _lastMouseNearWindowTime = now; // 记录鼠标靠近时间
                    RestoreFromEdge();
                }
            }
            else
            {
                // 窗口未隐藏时的处理逻辑
                
                // 1. 检查鼠标是否在窗口区域附近
                bool isMouseNearWindow = IsMouseNearWindow(mousePos, screen);
                
                if (isMouseNearWindow)
                {
                    // 鼠标在窗口附近，更新最近一次靠近时间
                    _lastMouseNearWindowTime = now;
                }
                else
                {
                    // 鼠标离开窗口区域，检查是否应该隐藏
                    if ((now - _lastMouseNearWindowTime).TotalMilliseconds >= HideDelayAfterMouseLeaveMs)
                    {
                        // 检查窗口是否贴近屏幕边缘
                        double left = this.Left;
                        double top = this.Top;
                        double right = left + this.Width;

                        // 左边缘吸附
                        if (left <= EdgeHideThreshold && left >= -this.Width + EdgeHideThreshold)
                        {
                            _edgeHideSide = "left";
                            if ((now - _lastEdgeActionTime).TotalMilliseconds >= cooldownMs)
                            {
                                _lastEdgeActionTime = now;
                                HideToEdge();
                            }
                        }
                        // 右边缘吸附
                        else if (right >= screen.Width - EdgeHideThreshold && right <= screen.Width + this.Width - EdgeHideThreshold)
                        {
                            _edgeHideSide = "right";
                            if ((now - _lastEdgeActionTime).TotalMilliseconds >= cooldownMs)
                            {
                                _lastEdgeActionTime = now;
                                HideToEdge();
                            }
                        }
                        // 顶部吸附
                        else if (top <= EdgeHideThreshold && top >= -this.ActualHeight + EdgeHideThreshold)
                        {
                            _edgeHideSide = "top";
                            if ((now - _lastEdgeActionTime).TotalMilliseconds >= cooldownMs)
                            {
                                _lastEdgeActionTime = now;
                                HideToEdge();
                            }
                        }
                    }
                }
            }
        }
        
        private bool IsMouseNearWindow(System.Drawing.Point mousePos, Rect screen)
        {
            // 检查鼠标是否在窗口或窗口附近（扩展50px）
            double nearThreshold = 50;
            return mousePos.X >= this.Left - nearThreshold &&
                   mousePos.X <= this.Left + this.Width + nearThreshold &&
                   mousePos.Y >= this.Top - nearThreshold &&
                   mousePos.Y <= this.Top + this.ActualHeight + nearThreshold;
        }

        private string _edgeHideSide = "left";

        private void HideToEdge()
        {
            if (_isEdgeHidden) return;

            _originalLeft = this.Left;
            _originalTop = this.Top;
            _originalWidth = this.Width;
            _originalHeight = this.ActualHeight;

            // 记录隐藏时的鼠标位置
            _mousePosWhenHidden = System.Windows.Forms.Cursor.Position;

            // 获取当前屏幕
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
            var screenBounds = screen.WorkingArea;

            switch (_edgeHideSide)
            {
                case "left":
                    this.Left = screenBounds.Left - this.Width + EdgeHideThreshold;
                    break;
                case "right":
                    this.Left = screenBounds.Right - EdgeHideThreshold;
                    break;
                case "top":
                    this.Top = screenBounds.Top - this.ActualHeight + EdgeHideThreshold;
                    break;
            }

            _isEdgeHidden = true;
        }

        private void RestoreFromEdge()
        {
            if (!_isEdgeHidden) return;

            this.Left = _originalLeft;
            this.Top = _originalTop;
            _isEdgeHidden = false;
            _mousePosWhenHidden = null;
        }

        private void ToggleHideWindow()
        {
            if (_isKeyHidden || _isUserHidden)
            {
                // 从快捷键隐藏状态恢复
                RestoreFromKeyHide();
            }
            else
            {
                // 快捷键隐藏
                HideByKey();
            }
        }

        private void HideByKey()
        {
            // 保存当前位置
            _originalLeft = this.Left;
            _originalTop = this.Top;
            _originalWidth = this.Width;
            _originalHeight = this.ActualHeight;

            _isKeyHidden = true;
            this.Hide();

            // 显示托盘提示
            _notifyIcon?.ShowBalloonTip(1000, "极简待办", "窗口已隐藏，按 Ctrl+H 恢复", System.Windows.Forms.ToolTipIcon.Info);
        }

        private void RestoreFromKeyHide()
        {
            _isKeyHidden = false;
            _isUserHidden = false;
            this.Show();
            this.Activate();
            this.WindowState = WindowState.Normal;

            // 恢复位置
            this.Left = _originalLeft;
            this.Top = _originalTop;

            EnsureWindowCorrectPosition();
        }

        #endregion

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleCompletedPanel(!_isCompletedVisible);
        }

        private void ToggleButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ToggleButtonBorder.Background = new SolidColorBrush(Color.FromArgb(0x66, 0x88, 0x88, 0x88));
            ToggleText.Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
        }

        private void ToggleButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ToggleButtonBorder.Background = Brushes.Transparent;
            ToggleText.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }

        private void RestoreRepeatingTasks()
        {
            var newTasks = TaskService.CheckAndRestoreRepeatingTasks(_allTasks);
            if (newTasks.Count > 0)
            {
                _allTasks.AddRange(newTasks);
                SaveData();
                RefreshTaskLists();
                _reminderService.UpdateTasks(_allTasks);
            }
        }

        private void LoadData()
        {
            _allTasks = DataService.LoadTasks();
            _settings = DataService.LoadSettings();
        }

        private void SaveData()
        {
            DataService.SaveTasks(_allTasks);
            DataService.SaveSettings(_settings);
        }

        private void SetupReminder()
        {
            _reminderService.UpdateTasks(_allTasks);
            _reminderService.UpdateAdvanceMinutes(_settings.AdvanceRemindMinutes);
            _reminderService.OnRemind += ShowReminder;
            _reminderService.Start();
        }

        private void SetupNotifyIcon()
        {
            _notifyIcon = NotifyIconService.CreateNotifyIcon(
                "极简透明桌面待办清单",
                (s, e) => ShowWindow(),
                (s, e) => ShowWindow(),
                (s, e) => OpenSettings(),
                (s, e) => ExitApplication()
            );
        }

        private void OpenSettings()
        {
            var dialog = new SettingsWindow(_settings, () => {
                LoadData();
                RefreshTaskLists();
                _reminderService.UpdateTasks(_allTasks);
            });
            dialog.Owner = this;
            _isShowingModal = true;
            try
            {
                if (dialog.ShowDialog() == true)
                {
                    var oldWallpaperMode = _settings.IsWallpaperMode;
                    var oldEdgeHide = _settings.IsEdgeHideEnabled;
                    _settings = dialog.Settings;

                    if (oldWallpaperMode != _settings.IsWallpaperMode)
                    {
                        if (_settings.IsWallpaperMode)
                            WallpaperModeService.EnableWallpaperMode(this);
                        else
                            WallpaperModeService.DisableWallpaperMode(this);
                    }

                    // 边缘隐藏快捷键变更
                    if (oldEdgeHide != _settings.IsEdgeHideEnabled)
                    {
                        if (_settings.IsEdgeHideEnabled)
                            RegisterGlobalHotKey();
                        else
                            UnregisterGlobalHotKey();
                    }

                    ApplySettings();
                    SaveData();
                    RefreshTaskLists();
                    _reminderService.UpdateTasks(_allTasks);
                    _reminderService.UpdateAdvanceMinutes(_settings.AdvanceRemindMinutes);
                }
            }
            finally
            {
                _isShowingModal = false;
            }
        }

        private void ExitApplication()
        {
            SaveData();
            _reminderService.Stop();
            UnregisterGlobalHotKey();
            _notifyIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void ShowWindow()
        {
            _isUserHidden = false;
            if (_isEdgeHidden)
                RestoreFromEdge();
            if (_isKeyHidden)
                RestoreFromKeyHide();
            this.Show();
            this.Activate();
            this.WindowState = WindowState.Normal;
            EnsureWindowCorrectPosition();
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            Win32Helper.HideFromTaskbar(this);
            
            // 确保窗口位置在屏幕可见区域
            var screen = SystemParameters.WorkArea;
            if (this.Left < 0 || this.Left + this.Width > screen.Width ||
                this.Top < 0 || this.Top + this.Height > screen.Height)
            {
                // 如果位置不可见，重置到默认位置
                this.Left = 100;
                this.Top = 100;
            }
            
            // 确保窗口可见
            this.Show();
            this.Visibility = Visibility.Visible;
            this.WindowState = WindowState.Normal;
            
            // 重置隐藏状态
            _isEdgeHidden = false;
            _isKeyHidden = false;
            _isUserHidden = false;
            
            EnsureWindowCorrectPosition();
            RegisterHotkey();

            if (_settings.IsWallpaperMode)
            {
                WallpaperModeService.EnableWallpaperMode(this);
            }
        }

        private void EnsureWindowCorrectPosition()
        {
            if (_settings.IsWallpaperMode)
            {
                WallpaperModeService.SetBottom(this);
            }
            else
            {
                Win32Helper.SetWindowTopMost(this, false);
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            _isUserHidden = true;
            this.Hide();
        }

        private void ShowReminder(TaskItem task, DateTime remindTime)
        {
            System.Windows.MessageBox.Show($"⏰ 任务提醒：\n\n{task.Content}\n\n提醒时间：{remindTime:yyyy-MM-dd HH:mm}",
                "待办提醒", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplySettings()
        {
            // 窗口整体不透明度保持为1（不再影响文字）
            this.Opacity = 1.0;
            this.FontFamily = new FontFamily(_settings.FontFamily);
            this.FontSize = _settings.FontSize;
            this.Left = _settings.WindowPosition.X;
            this.Top = _settings.WindowPosition.Y;
            this.Width = _settings.WindowSize.Width;
            this.Height = _settings.WindowSize.Height;

            ApplyThemeColor();
            ApplyTextOpacity();
            EnsureWindowCorrectPosition();
        }

        private void ApplyThemeColor()
        {
            Color bgColor;
            byte bgAlpha;

            switch (_settings.ThemeColor)
            {
                case ThemeColor.LightGreen:
                    bgColor = Color.FromRgb(144, 238, 144);
                    bgAlpha = (byte)(_settings.BackgroundOpacity * 255);
                    break;
                case ThemeColor.White:
                    bgColor = Color.FromRgb(255, 255, 255);
                    bgAlpha = (byte)(_settings.BackgroundOpacity * 255);
                    break;
                case ThemeColor.Gray:
                    bgColor = Color.FromRgb(128, 128, 128);
                    bgAlpha = (byte)(_settings.BackgroundOpacity * 255);
                    break;
                case ThemeColor.Dark:
                    bgColor = Color.FromRgb(30, 41, 59); // #1E293B
                    bgAlpha = 0xE5; // 深色主题使用较高不透明度
                    break;
                case ThemeColor.Custom:
                    bgColor = Color.FromRgb(_settings.CustomColor.R, _settings.CustomColor.G, _settings.CustomColor.B);
                    bgAlpha = (byte)(_settings.BackgroundOpacity * 255);
                    break;
                default:
                    bgColor = Color.FromRgb(30, 41, 59);
                    bgAlpha = 0xE5;
                    break;
            }

            var semiTransparentColor = Color.FromArgb(bgAlpha, bgColor.R, bgColor.G, bgColor.B);
            MainBorder.Background = new SolidColorBrush(semiTransparentColor);
        }

        private void ApplyTextOpacity()
        {
            // 文字透明度通过设置文字颜色的 alpha 通道实现
            byte textAlpha = (byte)(_settings.TextOpacity * 255);
            // 更新所有任务卡片的文字颜色
            foreach (var task in PendingTasks)
            {
                task.UpdateTextOpacity(_settings.TextOpacity);
            }
            foreach (var task in CompletedTasks)
            {
                task.UpdateTextOpacity(_settings.TextOpacity);
            }
        }

        private void BindData()
        {
            RefreshTaskLists();
            PendingTasksList.ItemsSource = PendingTasks;
            CompletedTasksList.ItemsSource = CompletedTasks;
        }

        private void RefreshTaskLists()
        {
            var selectedId = _selectedTask?.TaskId;

            PendingTasks.Clear();
            CompletedTasks.Clear();

            var pendingTasks = TaskService.GetPendingTasks(_allTasks, _settings.TaskSortType).ToList();

            // 分离置顶和非置顶任务
            var pinnedTasks = pendingTasks.Where(t => t.IsPinned).ToList();
            var normalTasks = pendingTasks.Where(t => !t.IsPinned).ToList();

            // 根据设置决定序号计算方式
            if (_settings.PinnedTaskNumberMode == PinnedTaskNumberMode.Separate)
            {
                // 单独排序：置顶和非置顶任务各自独立编号
                int pinnedNumber = 1;
                int pinnedColorIndex = 0;
                foreach (var task in pinnedTasks)
                {
                    var displayItem = new TaskDisplayItem(task, _settings, pinnedNumber++, true, pinnedColorIndex++);
                    if (task.TaskId == selectedId)
                        displayItem.IsSelected = true;
                    PendingTasks.Add(displayItem);
                }

                int normalNumber = 1;
                int normalColorIndex = 0;
                foreach (var task in normalTasks)
                {
                    var displayItem = new TaskDisplayItem(task, _settings, normalNumber++, true, normalColorIndex++);
                    if (task.TaskId == selectedId)
                        displayItem.IsSelected = true;
                    PendingTasks.Add(displayItem);
                }
            }
            else
            {
                // 统一排序：置顶与非置顶任务使用连续序号
                int unifiedNumber = 1;
                int unifiedColorIndex = 0;
                foreach (var task in pinnedTasks)
                {
                    var displayItem = new TaskDisplayItem(task, _settings, unifiedNumber++, true, unifiedColorIndex++);
                    if (task.TaskId == selectedId)
                        displayItem.IsSelected = true;
                    PendingTasks.Add(displayItem);
                }

                foreach (var task in normalTasks)
                {
                    var displayItem = new TaskDisplayItem(task, _settings, unifiedNumber++, true, unifiedColorIndex++);
                    if (task.TaskId == selectedId)
                        displayItem.IsSelected = true;
                    PendingTasks.Add(displayItem);
                }
            }

            foreach (var task in TaskService.GetCompletedTasks(_allTasks))
            {
                CompletedTasks.Add(new TaskDisplayItem(task, _settings));
            }
        }

        private void SetupContextMenu()
        {
            var contextMenu = new System.Windows.Controls.ContextMenu();
            
            var sortMenuItem = new System.Windows.Controls.MenuItem { Header = "排序方式" };
            
            var defaultSort = new System.Windows.Controls.MenuItem 
            { 
                Header = "默认排序（紧急、重要）", 
                IsChecked = _settings.TaskSortType == TaskSortType.Default 
            };
            defaultSort.Click += (s, e) => ChangeSortType(TaskSortType.Default);
            
            var createdSort = new System.Windows.Controls.MenuItem 
            { 
                Header = "创建时间排序", 
                IsChecked = _settings.TaskSortType == TaskSortType.CreatedTime 
            };
            createdSort.Click += (s, e) => ChangeSortType(TaskSortType.CreatedTime);
            
            var remindSort = new System.Windows.Controls.MenuItem 
            { 
                Header = "提醒时间排序", 
                IsChecked = _settings.TaskSortType == TaskSortType.RemindTime 
            };
            remindSort.Click += (s, e) => ChangeSortType(TaskSortType.RemindTime);
            
            sortMenuItem.Items.Add(defaultSort);
            sortMenuItem.Items.Add(createdSort);
            sortMenuItem.Items.Add(remindSort);
            contextMenu.Items.Add(sortMenuItem);
            
            // 为任务列表区域添加右键菜单
            PendingTasksList.ContextMenu = contextMenu;
        }

        private void ChangeSortType(TaskSortType sortType)
        {
            _settings.TaskSortType = sortType;
            RefreshTaskLists();
            SetupContextMenu(); // 刷新菜单选中状态
        }

        private void SetupDragMove()
        {
            this.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };

            this.LocationChanged += (s, e) =>
            {
                _settings.WindowPosition = new Point(this.Left, this.Top);
                EnsureWindowCorrectPosition();
            };

            this.SizeChanged += (s, e) =>
            {
                _settings.WindowSize = new Size(this.Width, this.Height);
            };
        }

        #region 任务选中状态

        private void TaskCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 整个卡片点击选中任务
            if (sender is FrameworkElement element && element.DataContext is TaskDisplayItem displayItem)
            {
                SelectTask(displayItem);
                e.Handled = true;
            }
        }

        private void DeselectAllTasks()
        {
            foreach (var task in PendingTasks)
            {
                task.IsSelected = false;
            }
            _selectedTask = null;
        }

        private void SelectTask(TaskDisplayItem displayItem)
        {
            // 如果点击的是已选中的任务，取消选中
            if (_selectedTask == displayItem)
            {
                displayItem.IsSelected = false;
                _selectedTask = null;
                return;
            }

            // 取消之前的选中
            DeselectAllTasks();

            // 选中新任务
            displayItem.IsSelected = true;
            _selectedTask = displayItem;
        }

        #endregion

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TaskEditWindow();
            dialog.Owner = this;
            _isShowingModal = true;
            try
            {
                if (dialog.ShowDialog() == true)
                {
                    var newTask = TaskService.CreateTask(
                        dialog.TaskContent,
                        dialog.TaskRemindTime,
                        dialog.TaskPriority,
                        dialog.TaskRepeat,
                        dialog.TaskRepeatDetail,
                        dialog.TaskAdditionalRemindTimes,
                        dialog.TaskTargetDate);

                    newTask.SubTasks = dialog.TaskSubTasks;

                    _allTasks.Add(newTask);
                    SaveData();
                    RefreshTaskLists();
                    _reminderService.UpdateTasks(_allTasks);
                }
            }
            finally
            {
                _isShowingModal = false;
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is TaskDisplayItem displayItem)
            {
                var task = _allTasks.FirstOrDefault(t => t.TaskId == displayItem.TaskId);
                if (task != null)
                {
                    var dialog = new TaskEditWindow(task);
                    dialog.Owner = this;
                    _isShowingModal = true;
                    try
                    {
                        if (dialog.ShowDialog() == true)
                        {
                            task.Content = dialog.TaskContent;
                            task.SubTasks = dialog.TaskSubTasks;
                            task.RemindTime = dialog.TaskRemindTime;
                            task.AdditionalRemindTimes = dialog.TaskAdditionalRemindTimes;
                            task.TargetDate = dialog.TaskTargetDate;
                            task.Priority = dialog.TaskPriority;
                            task.Repeat = dialog.TaskRepeat;
                            task.RepeatDetail = dialog.TaskRepeatDetail;
                            SaveData();
                            RefreshTaskLists();
                            _reminderService.UpdateTasks(_allTasks);
                        }
                    }
                    finally
                    {
                        _isShowingModal = false;
                    }
                }
            }
        }

        private void BtnComplete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is TaskDisplayItem displayItem)
            {
                var task = _allTasks.FirstOrDefault(t => t.TaskId == displayItem.TaskId);
                if (task != null)
                {
                    var completedTask = TaskService.CompleteTask(task, out var newTask);
                    if (completedTask != null)
                    {
                        _allTasks.Remove(task);
                        _allTasks.Add(completedTask);
                        if (newTask != null)
                        {
                            _allTasks.Add(newTask);
                        }
                        SaveData();
                        RefreshTaskLists();
                        _reminderService.UpdateTasks(_allTasks);
                        _reminderService.ClearReminded(task.TaskId);
                    }
                }
            }
        }

        private void SubTaskCheck_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.Tag is SubTaskDisplayItem subDisplay)
            {
                var taskDisplay = FindParentTaskDisplayItem(checkBox);
                if (taskDisplay != null)
                {
                    var task = _allTasks.FirstOrDefault(t => t.TaskId == taskDisplay.TaskId);
                    if (task != null && subDisplay.Index >= 0 && subDisplay.Index < task.SubTasks.Count)
                    {
                        task.SubTasks[subDisplay.Index].IsChecked = checkBox.IsChecked == true;
                        SaveData();
                        taskDisplay.UpdateSubTaskProgress(task.SubTasks);
                    }
                }
            }
        }

        private void ToggleSubTasks_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is TaskDisplayItem displayItem)
            {
                displayItem.ToggleSubTasks();
                SelectTask(displayItem);
                e.Handled = true;
            }
        }

        private void TaskContent_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 获取点击的 StackPanel 的 DataContext
            if (sender is FrameworkElement element && element.DataContext is TaskDisplayItem displayItem)
            {
                SelectTask(displayItem);
                e.Handled = true;
            }
        }

        private TaskDisplayItem? FindParentTaskDisplayItem(DependencyObject child)
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is FrameworkElement fe && fe.DataContext is TaskDisplayItem tdi)
                {
                    return tdi;
                }
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is TaskDisplayItem displayItem)
            {
                var result = System.Windows.MessageBox.Show("确定要删除这个任务吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    var task = _allTasks.FirstOrDefault(t => t.TaskId == displayItem.TaskId);
                    if (task != null)
                    {
                        _allTasks.Remove(task);
                        SaveData();
                        _selectedTask = null;
                        RefreshTaskLists();
                        _reminderService.UpdateTasks(_allTasks);
                    }
                }
            }
        }

        private void BtnPin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is TaskDisplayItem displayItem)
            {
                var task = _allTasks.FirstOrDefault(t => t.TaskId == displayItem.TaskId);
                if (task != null)
                {
                    task.IsPinned = !task.IsPinned;
                    SaveData();
                    RefreshTaskLists();
                }
            }
        }

        private void BtnQuickAddReminder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is TaskDisplayItem displayItem)
            {
                var task = _allTasks.FirstOrDefault(t => t.TaskId == displayItem.TaskId);
                if (task != null)
                {
                    // 快速添加一个提醒时间：在主提醒时间基础上加1小时
                    var baseTime = task.RemindTime ?? DateTime.Now.AddHours(1);
                    var newRemindTime = baseTime.AddHours(1);

                    if (task.AdditionalRemindTimes == null)
                        task.AdditionalRemindTimes = new List<DateTime>();

                    task.AdditionalRemindTimes.Add(newRemindTime);
                    SaveData();
                    RefreshTaskLists();
                    _reminderService.UpdateTasks(_allTasks);

                    System.Windows.MessageBox.Show($"已添加提醒时间：{newRemindTime:yyyy-MM-dd HH:mm}\n\n如需修改，请使用编辑功能。",
                        "提醒已添加", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnDeleteCompleted_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is TaskDisplayItem displayItem)
            {
                var result = System.Windows.MessageBox.Show("确定要删除这个已完成的任务吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    var task = _allTasks.FirstOrDefault(t => t.TaskId == displayItem.TaskId);
                    if (task != null)
                    {
                        _allTasks.Remove(task);
                        SaveData();
                        RefreshTaskLists();
                    }
                }
            }
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is TaskDisplayItem displayItem)
            {
                var task = _allTasks.FirstOrDefault(t => t.TaskId == displayItem.TaskId);
                if (task != null)
                {
                    // 恢复任务：取消完成状态
                    task.IsCompleted = false;
                    task.CompletedTime = null;
                    SaveData();
                    RefreshTaskLists();
                    _reminderService.UpdateTasks(_allTasks);
                }
            }
        }

        private void BtnClearCompleted_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show("确定要清空所有已完成任务吗？（循环任务不会被删除）", "确认清空", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                var completedTasks = TaskService.GetCompletedTasks(_allTasks).Where(t => t.Repeat == RepeatType.None).ToList();
                foreach (var task in completedTasks)
                {
                    _allTasks.Remove(task);
                }
                SaveData();
                RefreshTaskLists();
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        protected override void OnClosed(EventArgs e)
        {
            _windowStateTimer?.Stop();
            SaveData();
            _reminderService.Stop();
            UnregisterGlobalHotKey();
            _notifyIcon?.Dispose();
            base.OnClosed(e);
        }
    }

    /// <summary>
    /// 次级清单展示项
    /// </summary>
    public class SubTaskDisplayItem
    {
        public int Index { get; }
        public string Text { get; }
        public bool IsChecked { get; set; }

        public SubTaskDisplayItem(int index, SubTaskItem subTask)
        {
            Index = index;
            Text = subTask.Text;
            IsChecked = subTask.IsChecked;
        }
    }

    /// <summary>
    /// 任务展示模型
    /// </summary>
    public class TaskDisplayItem : System.ComponentModel.INotifyPropertyChanged
    {
        public Guid TaskId { get; }
        public string Content { get; }
        public string? RemindTimeText { get; }
        public string? CompletedTimeText { get; }
        public string PriorityColor { get; }
        public string PriorityText { get; }
        public string RepeatShortLabel { get; }
        public string RepeatColor { get; }
        public bool HasRepeat { get; }
        public System.Windows.Media.Brush RepeatLabelBackground { get; }
        public System.Windows.Visibility ShowRepeatLabelVis { get; }
        public bool IsPinned { get; }
        public System.Windows.Visibility IsPinnedVis => IsPinned ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public bool HasRemind { get; }
        public bool IsUrgent { get; }

        // 字体颜色
        public System.Windows.Media.Brush TextColorBrush { get; }

        // 任务序号
        public int TaskNumber { get; }
        public string TaskNumberText => TaskNumber.ToString();
        public bool ShowNumber { get; }
        public System.Windows.Media.Brush TaskNumberBackground { get; }  // 序号圆点底色

        // 今日任务标记
        public bool IsTodayTask { get; }

        // 创建时间
        public string CreatedTimeText { get; }
        public bool ShowCreatedTime { get; }
        public System.Windows.Visibility CreatedTimeVisibility => ShowCreatedTime ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        // 日期倒数
        public string CountdownText { get; }
        public string CountdownColor { get; }
        public bool HasCountdown { get; }

        // 目标日期文本
        public string TargetDateText { get; }

        // 额外提醒时间
        public List<string> AdditionalRemindTimeTexts { get; } = new List<string>();
        public string AdditionalRemindTimeSummary { get; } = "";

        // 短格式提醒时间（未选中时显示）
        public string RemindTimeShortText { get; }

        // 选中状态
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
                OnPropertyChanged(nameof(IsNotSelected));
            }
        }

        public bool IsNotSelected => !IsSelected;

        public ICommand SelectCommand { get; }

        // 次级清单相关
        public bool HasSubTasks { get; private set; }
        public List<SubTaskDisplayItem> SubTasks { get; private set; } = new List<SubTaskDisplayItem>();
        public string SubTaskProgressText { get; private set; } = "";
        public bool AllSubTasksCompleted { get; private set; }

        // 展开/折叠状态
        private bool _isSubTasksExpanded = false;
        public bool IsSubTasksExpanded
        {
            get => _isSubTasksExpanded;
            set
            {
                _isSubTasksExpanded = value;
                OnPropertyChanged(nameof(IsSubTasksExpanded));
                OnPropertyChanged(nameof(SubTasksVisibility));
                OnPropertyChanged(nameof(ExpandButtonText));
            }
        }

        public System.Windows.Visibility SubTasksVisibility =>
            (HasSubTasks && IsSubTasksExpanded) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public string ExpandButtonText => IsSubTasksExpanded ? "▼" : "▶";

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        public TaskDisplayItem(TaskItem task, AppSettings settings, int taskNumber = 0, bool showNumber = false, int colorIndex = 0)
        {
            TaskId = task.TaskId;
            Content = task.Content;
            RemindTimeText = task.RemindTime.HasValue ? $"⏰ {task.RemindTime:yyyy-MM-dd HH:mm}" : "";
            RemindTimeShortText = task.RemindTime.HasValue ? $"{task.RemindTime:MM-dd HH:mm}" : "";
            CompletedTimeText = task.CompletedTime.HasValue ? $"✅ {task.CompletedTime:yyyy-MM-dd HH:mm}" : "";
            PriorityColor = TaskService.GetPriorityColor(task.Priority);
            PriorityText = TaskService.GetPriorityText(task.Priority);
            RepeatShortLabel = TaskService.GetRepeatShortLabel(task.Repeat);
            RepeatColor = TaskService.GetRepeatColor(task.Repeat, settings);
            HasRepeat = task.Repeat != RepeatType.None;

            // 循环标记：默认隐藏，设置中可开启
            ShowRepeatLabelVis = (HasRepeat && settings.ShowRepeatLabel) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            try
            {
                var converter = new System.Windows.Media.BrushConverter();
                RepeatLabelBackground = (System.Windows.Media.Brush)converter.ConvertFromString(RepeatColor) ?? System.Windows.Media.Brushes.Gray;
            }
            catch
            {
                RepeatLabelBackground = System.Windows.Media.Brushes.Gray;
            }

            IsPinned = task.IsPinned;
            HasRemind = task.RemindTime.HasValue;
            IsUrgent = task.Priority == PriorityLevel.Urgent;

            // 任务序号
            TaskNumber = taskNumber;
            ShowNumber = showNumber && taskNumber > 0;

            // 序号圆点底色（循环颜色）
            try
            {
                var converter = new System.Windows.Media.BrushConverter();
                var colors = settings.TaskNumberColors;
                if (colors != null && colors.Count > 0)
                {
                    var color = colors[colorIndex % colors.Count];
                    TaskNumberBackground = (System.Windows.Media.Brush)converter.ConvertFromString(color) ?? System.Windows.Media.Brushes.Gray;
                }
                else
                {
                    TaskNumberBackground = System.Windows.Media.Brushes.Gray;
                }
            }
            catch
            {
                TaskNumberBackground = System.Windows.Media.Brushes.Gray;
            }

            // 今日任务判断（提醒时间是今天，或目标日期是今天）
            var today = DateTime.Today;
            IsTodayTask = (task.RemindTime.HasValue && task.RemindTime.Value.Date == today) ||
                          (task.TargetDate.HasValue && task.TargetDate.Value.Date == today);

            // 创建时间
            CreatedTimeText = $"📝 {task.CreatedTime:yyyy-MM-dd HH:mm}";
            ShowCreatedTime = settings.ShowCreatedTime;

            // 字体颜色
            try
            {
                var converter = new System.Windows.Media.BrushConverter();
                TextColorBrush = (System.Windows.Media.Brush)converter.ConvertFromString(settings.TextColor) ?? System.Windows.Media.Brushes.White;
            }
            catch
            {
                TextColorBrush = System.Windows.Media.Brushes.White;
            }

            // 日期倒数
            CountdownText = TaskService.GetCountdownText(task.TargetDate);
            CountdownColor = TaskService.GetCountdownColor(task.TargetDate);
            HasCountdown = task.TargetDate.HasValue;
            TargetDateText = task.TargetDate.HasValue ? task.TargetDate.Value.ToString("yyyy-MM-dd") : "未设置";

            // 额外提醒时间
            if (task.AdditionalRemindTimes != null)
            {
                foreach (var rt in task.AdditionalRemindTimes)
                {
                    AdditionalRemindTimeTexts.Add($"⏰ {rt:yyyy-MM-dd HH:mm}");
                }
                if (task.AdditionalRemindTimes.Count > 0)
                {
                    AdditionalRemindTimeSummary = $"  (+{task.AdditionalRemindTimes.Count}个提醒)";
                }
            }

            // 选中命令
            SelectCommand = new RelayCommand(param =>
            {
                // 通过事件冒泡由 MainWindow 处理
            });

            if (task.SubTasks != null && task.SubTasks.Count > 0)
            {
                HasSubTasks = true;
                for (int i = 0; i < task.SubTasks.Count; i++)
                {
                    SubTasks.Add(new SubTaskDisplayItem(i, task.SubTasks[i]));
                }
                UpdateSubTaskProgress(task.SubTasks);
            }
        }

        public void UpdateSubTaskProgress(List<SubTaskItem> subTasks)
        {
            if (subTasks == null || subTasks.Count == 0) return;

            int completed = subTasks.Count(s => s.IsChecked);
            int total = subTasks.Count;
            SubTaskProgressText = $"{completed}/{total}";
            AllSubTasksCompleted = completed == total;

            for (int i = 0; i < SubTasks.Count && i < subTasks.Count; i++)
            {
                SubTasks[i].IsChecked = subTasks[i].IsChecked;
            }

            OnPropertyChanged(nameof(SubTaskProgressText));
        }

        public void UpdateTextOpacity(double opacity)
        {
            // 文字透明度在 WPF 中通过 Opacity 属性实现
            // 这里主要用于标记，实际渲染由 XAML 样式控制
        }

        public void ToggleSubTasks()
        {
            if (HasSubTasks)
            {
                IsSubTasksExpanded = !IsSubTasksExpanded;
            }
        }
    }

    /// <summary>
    /// 简单的 RelayCommand 实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
