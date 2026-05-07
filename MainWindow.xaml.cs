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
        private double _normalWidth = 500;
        private double _expandedWidth = 800;

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
            this.SourceInitialized += OnSourceInitialized;
            this.Activated += (s, e) => EnsureWindowCorrectPosition();
        }

        private void SetupWindowStateTimer()
        {
            _windowStateTimer = new System.Windows.Threading.DispatcherTimer();
            _windowStateTimer.Interval = TimeSpan.FromMilliseconds(50);
            _windowStateTimer.Tick += OnWindowStateTimerTick;
            _windowStateTimer.Start();
        }

        private void OnWindowStateTimerTick(object? sender, EventArgs e)
        {
            if (_isShowingModal)
                return;

            if (_settings.IsWallpaperMode)
            {
                WallpaperModeService.ForceKeepVisible(this, _isUserHidden);
            }

            _repeatCheckCounter++;
            if (_repeatCheckCounter >= 1000)
            {
                _repeatCheckCounter = 0;
                RestoreRepeatingTasks();
            }
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isCompletedVisible = !_isCompletedVisible;
            if (_isCompletedVisible)
            {
                CompletedColumn.Width = new GridLength(1, GridUnitType.Star);
                CompletedPanel.Visibility = Visibility.Visible;
                ToggleText.Text = "«";
                this.Width = _expandedWidth;
            }
            else
            {
                CompletedColumn.Width = new GridLength(0);
                CompletedPanel.Visibility = Visibility.Collapsed;
                ToggleText.Text = "»";
                this.Width = _normalWidth;
            }
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
                    _settings = dialog.Settings;

                    if (oldWallpaperMode != _settings.IsWallpaperMode)
                    {
                        if (_settings.IsWallpaperMode)
                        {
                            WallpaperModeService.EnableWallpaperMode(this);
                        }
                        else
                        {
                            WallpaperModeService.DisableWallpaperMode(this);
                        }
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
            _notifyIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void ShowWindow()
        {
            _isUserHidden = false;
            this.Show();
            this.Activate();
            this.WindowState = WindowState.Normal;
            EnsureWindowCorrectPosition();
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            Win32Helper.HideFromTaskbar(this);
            EnsureWindowCorrectPosition();

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

        private void ShowReminder(TaskItem task)
        {
            System.Windows.MessageBox.Show($"⏰ 任务提醒：\n\n{task.Content}\n\n提醒时间：{task.RemindTime:yyyy-MM-dd HH:mm}",
                "待办提醒", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplySettings()
        {
            this.Opacity = _settings.WindowOpacity;
            this.FontFamily = new FontFamily(_settings.FontFamily);
            this.FontSize = _settings.FontSize;
            this.Left = _settings.WindowPosition.X;
            this.Top = _settings.WindowPosition.Y;
            this.Width = _settings.WindowSize.Width;
            this.Height = _settings.WindowSize.Height;

            ApplyThemeColor();
            EnsureWindowCorrectPosition();
        }

        private void ApplyThemeColor()
        {
            Color bgColor;
            switch (_settings.ThemeColor)
            {
                case ThemeColor.LightGreen:
                    bgColor = Color.FromRgb(144, 238, 144);
                    break;
                case ThemeColor.White:
                    bgColor = Color.FromRgb(255, 255, 255);
                    break;
                case ThemeColor.Gray:
                    bgColor = Color.FromRgb(128, 128, 128);
                    break;
                case ThemeColor.Custom:
                    bgColor = Color.FromRgb(_settings.CustomColor.R, _settings.CustomColor.G, _settings.CustomColor.B);
                    break;
                default:
                    bgColor = Color.FromRgb(144, 238, 144);
                    break;
            }

            var semiTransparentColor = Color.FromArgb(0xCC, bgColor.R, bgColor.G, bgColor.B);
            var semiTransparentBrush = new SolidColorBrush(semiTransparentColor);
            MainBorder.Background = semiTransparentBrush;
        }

        private void BindData()
        {
            RefreshTaskLists();
            PendingTasksList.ItemsSource = PendingTasks;
            CompletedTasksList.ItemsSource = CompletedTasks;
        }

        private void RefreshTaskLists()
        {
            PendingTasks.Clear();
            CompletedTasks.Clear();

            foreach (var task in TaskService.GetPendingTasks(_allTasks))
            {
                PendingTasks.Add(new TaskDisplayItem(task, _settings));
            }

            foreach (var task in TaskService.GetCompletedTasks(_allTasks))
            {
                CompletedTasks.Add(new TaskDisplayItem(task, _settings));
            }
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
            };

            this.SizeChanged += (s, e) =>
            {
                _settings.WindowSize = new Size(this.Width, this.Height);
            };
        }

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
                        dialog.TaskRepeatDetail);

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
                // 通过可视化树向上找到 TaskDisplayItem（作为 ItemsControl 的 DataContext）
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
                e.Handled = true; // 防止事件冒泡
            }
        }

        private void TaskContent_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBlock textBlock && textBlock.Tag is TaskDisplayItem displayItem)
            {
                if (displayItem.HasSubTasks)
                {
                    displayItem.ToggleSubTasks();
                }
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
                        RefreshTaskLists();
                        _reminderService.UpdateTasks(_allTasks);
                    }
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

        public TaskDisplayItem(TaskItem task, AppSettings settings)
        {
            TaskId = task.TaskId;
            Content = task.Content;
            RemindTimeText = task.RemindTime.HasValue ? $"⏰ {task.RemindTime:yyyy-MM-dd HH:mm}" : "";
            CompletedTimeText = task.CompletedTime.HasValue ? $"✅ {task.CompletedTime:yyyy-MM-dd HH:mm}" : "";
            PriorityColor = TaskService.GetPriorityColor(task.Priority);
            PriorityText = TaskService.GetPriorityText(task.Priority);
            RepeatShortLabel = TaskService.GetRepeatShortLabel(task.Repeat);
            RepeatColor = TaskService.GetRepeatColor(task.Repeat, settings);
            HasRepeat = task.Repeat != RepeatType.None;

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

            // 同步更新展示项的勾选状态
            for (int i = 0; i < SubTasks.Count && i < subTasks.Count; i++)
            {
                SubTasks[i].IsChecked = subTasks[i].IsChecked;
            }

            OnPropertyChanged(nameof(SubTaskProgressText));
        }

        public void ToggleSubTasks()
        {
            if (HasSubTasks)
            {
                IsSubTasksExpanded = !IsSubTasksExpanded;
            }
        }
    }
}
