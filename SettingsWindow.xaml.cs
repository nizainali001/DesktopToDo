using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using DesktopToDo.Models;
using DesktopToDo.Services;

namespace DesktopToDo
{
    public partial class SettingsWindow : Window
    {
        public AppSettings Settings { get; private set; }
        private readonly Action _refreshAction;

        public SettingsWindow(AppSettings currentSettings, Action refreshAction = null)
        {
            InitializeComponent();
            Settings = new AppSettings
            {
                WindowOpacity = currentSettings.WindowOpacity,
                BackgroundOpacity = currentSettings.BackgroundOpacity,
                TextOpacity = currentSettings.TextOpacity,
                FontFamily = currentSettings.FontFamily,
                FontSize = currentSettings.FontSize,
                IsWallpaperMode = currentSettings.IsWallpaperMode,
                IsAutoStart = currentSettings.IsAutoStart,
                AdvanceRemindMinutes = currentSettings.AdvanceRemindMinutes,
                WindowPosition = currentSettings.WindowPosition,
                WindowSize = currentSettings.WindowSize,
                ThemeColor = currentSettings.ThemeColor,
                CustomColor = currentSettings.CustomColor,
                IsEdgeHideEnabled = currentSettings.IsEdgeHideEnabled,
                EdgeHideHotkey = currentSettings.EdgeHideHotkey,
                TextColor = currentSettings.TextColor,
                DailyRepeatColor = currentSettings.DailyRepeatColor,
                WeeklyRepeatColor = currentSettings.WeeklyRepeatColor,
                MonthlyRepeatColor = currentSettings.MonthlyRepeatColor,
                QuarterlyRepeatColor = currentSettings.QuarterlyRepeatColor,
                YearlyRepeatColor = currentSettings.YearlyRepeatColor,
                WorkdaysRepeatColor = currentSettings.WorkdaysRepeatColor,
                TaskSortType = currentSettings.TaskSortType,
                ShowCreatedTime = currentSettings.ShowCreatedTime,
                ShowRepeatLabel = currentSettings.ShowRepeatLabel,
                PinnedTaskNumberMode = currentSettings.PinnedTaskNumberMode
            };
            _refreshAction = refreshAction;

            SliderBgOpacity.Value = Settings.BackgroundOpacity;
            SliderTextOpacity.Value = Settings.TextOpacity;
            SliderFontSize.Value = Settings.FontSize;
            TxtTextColor.Text = Settings.TextColor;
            UpdateTextColorPreview();
            TxtAdvanceMinutes.Text = Settings.AdvanceRemindMinutes.ToString();
            ChkAutoStart.IsChecked = Settings.IsAutoStart;
            ChkWallpaperMode.IsChecked = Settings.IsWallpaperMode;
            ChkEdgeHide.IsChecked = Settings.IsEdgeHideEnabled;
            ChkShowCreatedTime.IsChecked = Settings.ShowCreatedTime;
            ChkShowRepeatLabel.IsChecked = Settings.ShowRepeatLabel;

            // 置顶序号排序模式
            CmbPinnedNumberMode.SelectedIndex = Settings.PinnedTaskNumberMode == PinnedTaskNumberMode.Separate ? 1 : 0;

            switch (Settings.ThemeColor)
            {
                case ThemeColor.LightGreen:
                    RbLightGreen.IsChecked = true;
                    break;
                case ThemeColor.White:
                    RbWhite.IsChecked = true;
                    break;
                case ThemeColor.Gray:
                    RbGray.IsChecked = true;
                    break;
                case ThemeColor.Dark:
                    RbDark.IsChecked = true;
                    break;
                case ThemeColor.Custom:
                    RbCustom.IsChecked = true;
                    CustomColorPanel.Visibility = Visibility.Visible;
                    break;
            }

            TxtR.Text = Settings.CustomColor.R.ToString();
            TxtG.Text = Settings.CustomColor.G.ToString();
            TxtB.Text = Settings.CustomColor.B.ToString();

            TxtDailyColor.Text = Settings.DailyRepeatColor;
            TxtWeeklyColor.Text = Settings.WeeklyRepeatColor;
            TxtMonthlyColor.Text = Settings.MonthlyRepeatColor;
            TxtQuarterlyColor.Text = Settings.QuarterlyRepeatColor;
            TxtYearlyColor.Text = Settings.YearlyRepeatColor;
            TxtWorkdaysColor.Text = Settings.WorkdaysRepeatColor;

            // 序号圆点颜色
            var colors = Settings.TaskNumberColors;
            if (colors != null && colors.Count >= 6)
            {
                TxtNumberColor1.Text = colors[0];
                TxtNumberColor2.Text = colors[1];
                TxtNumberColor3.Text = colors[2];
                TxtNumberColor4.Text = colors[3];
                TxtNumberColor5.Text = colors[4];
                TxtNumberColor6.Text = colors[5];
            }

            UpdateBgOpacityText();
            UpdateTextOpacityText();
            UpdateFontSizeText();
        }

        private void SliderBgOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateBgOpacityText();
        }

        private void UpdateBgOpacityText()
        {
            if (TxtBgOpacityValue != null)
            {
                TxtBgOpacityValue.Text = $"{(int)(SliderBgOpacity.Value * 100)}%";
            }
        }

        private void SliderTextOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateTextOpacityText();
        }

        private void UpdateTextOpacityText()
        {
            if (TxtTextOpacityValue != null)
            {
                TxtTextOpacityValue.Text = $"{(int)(SliderTextOpacity.Value * 100)}%";
            }
        }

        private void SliderFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateFontSizeText();
        }

        private void UpdateFontSizeText()
        {
            if (TxtFontSizeValue != null)
            {
                TxtFontSizeValue.Text = $"{(int)SliderFontSize.Value}";
            }
        }

        private void UpdateTextColorPreview()
        {
            if (TextColorPreview != null && TxtTextColor != null)
            {
                try
                {
                    var brush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(TxtTextColor.Text);
                    TextColorPreview.Background = brush;
                }
                catch
                {
                    TextColorPreview.Background = System.Windows.Media.Brushes.Gray;
                }
            }
        }

        private void RbCustom_Checked(object sender, RoutedEventArgs e)
        {
            CustomColorPanel.Visibility = Visibility.Visible;
        }

        private void RbCustom_Unchecked(object sender, RoutedEventArgs e)
        {
            CustomColorPanel.Visibility = Visibility.Collapsed;
        }

        private void TxtTextColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTextColorPreview();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                DefaultExt = "json",
                FileName = $"tasks_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    DataService.ExportTasks(dialog.FileName);
                    System.Windows.MessageBox.Show("导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                DefaultExt = "json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var importedTasks = DataService.ImportTasks(dialog.FileName);
                    var result = System.Windows.MessageBox.Show($"将导入 {importedTasks.Count} 个任务，是否合并到现有任务？\n\n是 - 合并\n否 - 替换全部", "导入确认", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes || result == MessageBoxResult.No)
                    {
                        if (result == MessageBoxResult.No)
                        {
                            var tasks = importedTasks;
                            DataService.SaveTasks(tasks);
                        }
                        else
                        {
                            var existingTasks = DataService.LoadTasks();
                            existingTasks.AddRange(importedTasks);
                            DataService.SaveTasks(existingTasks);
                        }

                        _refreshAction?.Invoke();
                        System.Windows.MessageBox.Show("导入成功！请点击确定保存设置后刷新。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"导入失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            Settings.BackgroundOpacity = SliderBgOpacity.Value;
            Settings.TextOpacity = SliderTextOpacity.Value;
            Settings.FontSize = SliderFontSize.Value;
            Settings.IsAutoStart = ChkAutoStart.IsChecked == true;
            Settings.IsWallpaperMode = ChkWallpaperMode.IsChecked == true;
            Settings.IsEdgeHideEnabled = ChkEdgeHide.IsChecked == true;
            Settings.ShowCreatedTime = ChkShowCreatedTime.IsChecked == true;
            Settings.ShowRepeatLabel = ChkShowRepeatLabel.IsChecked == true;

            // 置顶序号排序模式
            Settings.PinnedTaskNumberMode = CmbPinnedNumberMode.SelectedIndex == 1 
                ? PinnedTaskNumberMode.Separate 
                : PinnedTaskNumberMode.Unified;

            if (int.TryParse(TxtAdvanceMinutes.Text, out int minutes))
            {
                Settings.AdvanceRemindMinutes = Math.Max(0, minutes);
            }

            if (RbLightGreen.IsChecked == true)
            {
                Settings.ThemeColor = ThemeColor.LightGreen;
            }
            else if (RbWhite.IsChecked == true)
            {
                Settings.ThemeColor = ThemeColor.White;
            }
            else if (RbGray.IsChecked == true)
            {
                Settings.ThemeColor = ThemeColor.Gray;
            }
            else if (RbDark.IsChecked == true)
            {
                Settings.ThemeColor = ThemeColor.Dark;
            }
            else if (RbCustom.IsChecked == true)
            {
                Settings.ThemeColor = ThemeColor.Custom;
                if (byte.TryParse(TxtR.Text, out byte r)) Settings.CustomColor.R = r;
                if (byte.TryParse(TxtG.Text, out byte g)) Settings.CustomColor.G = g;
                if (byte.TryParse(TxtB.Text, out byte b)) Settings.CustomColor.B = b;
            }

            Settings.TextColor = IsValidColor(TxtTextColor.Text) ? TxtTextColor.Text : "#F1F5F9";

            Settings.DailyRepeatColor = IsValidColor(TxtDailyColor.Text) ? TxtDailyColor.Text : "#00FF00";
            Settings.WeeklyRepeatColor = IsValidColor(TxtWeeklyColor.Text) ? TxtWeeklyColor.Text : "#00FF00";
            Settings.MonthlyRepeatColor = IsValidColor(TxtMonthlyColor.Text) ? TxtMonthlyColor.Text : "#FF0000";
            Settings.QuarterlyRepeatColor = IsValidColor(TxtQuarterlyColor.Text) ? TxtQuarterlyColor.Text : "#FF0000";
            Settings.YearlyRepeatColor = IsValidColor(TxtYearlyColor.Text) ? TxtYearlyColor.Text : "#000000";
            Settings.WorkdaysRepeatColor = IsValidColor(TxtWorkdaysColor.Text) ? TxtWorkdaysColor.Text : "#00FF00";

            // 序号圆点颜色
            Settings.TaskNumberColors = new List<string>
            {
                IsValidColor(TxtNumberColor1.Text) ? TxtNumberColor1.Text : "#90EE90",
                IsValidColor(TxtNumberColor2.Text) ? TxtNumberColor2.Text : "#87CEEB",
                IsValidColor(TxtNumberColor3.Text) ? TxtNumberColor3.Text : "#FBBF24",
                IsValidColor(TxtNumberColor4.Text) ? TxtNumberColor4.Text : "#FF6B6B",
                IsValidColor(TxtNumberColor5.Text) ? TxtNumberColor5.Text : "#DDA0DD",
                IsValidColor(TxtNumberColor6.Text) ? TxtNumberColor6.Text : "#FFB6C1"
            };

            SetAutoStart(Settings.IsAutoStart);
            this.DialogResult = true;
            this.Close();
        }

        private void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    if (enable)
                    {
                        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            key.SetValue("DesktopToDo", exePath);
                        }
                    }
                    else
                    {
                        if (key.GetValue("DesktopToDo") != null)
                        {
                            key.DeleteValue("DesktopToDo");
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private bool IsValidColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color) || color.Length != 7 || !color.StartsWith("#"))
                return false;
            try
            {
                var brush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color);
                return brush != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
