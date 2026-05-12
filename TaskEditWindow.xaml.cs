using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopToDo.Models;
using DesktopToDo.Services;

namespace DesktopToDo
{
    public partial class TaskEditWindow : Window
    {
        public string TaskContent { get; private set; } = string.Empty;
        public List<SubTaskItem> TaskSubTasks { get; private set; } = new List<SubTaskItem>();
        public DateTime? TaskRemindTime { get; private set; }
        public List<DateTime> TaskAdditionalRemindTimes { get; private set; } = new List<DateTime>();
        public DateTime? TaskTargetDate { get; private set; }
        public PriorityLevel TaskPriority { get; private set; }
        public RepeatType TaskRepeat { get; private set; }
        public RepeatRuleDetail TaskRepeatDetail { get; private set; } = new RepeatRuleDetail();

        private bool _isLoadingSettings = false;
        private List<SubTaskItem>? _originalSubTasks;
        private ObservableCollection<AdditionalReminderItem> _additionalReminders = new ObservableCollection<AdditionalReminderItem>();

        public TaskEditWindow(TaskItem? task = null)
        {
            InitializeComponent();
            InitializeCombos();

            DpRemindDate.SelectedDate = DateTime.Now;
            AdditionalRemindersList.ItemsSource = _additionalReminders;

            if (task != null)
            {
                this.Title = "编辑任务";
                _originalSubTasks = task.SubTasks;

                if (task.SubTasks != null && task.SubTasks.Count > 0)
                {
                    var lines = new List<string> { task.Content };
                    foreach (var sub in task.SubTasks)
                    {
                        var prefix = sub.IsChecked ? "☑ " : "";
                        lines.Add(prefix + sub.Text);
                    }
                    TxtContent.Text = string.Join(Environment.NewLine, lines);
                }
                else
                {
                    TxtContent.Text = task.Content;
                }

                ChkHasRemind.IsChecked = task.RemindTime.HasValue;
                if (task.RemindTime.HasValue)
                {
                    DpRemindDate.SelectedDate = task.RemindTime.Value.Date;
                    TxtRemindTime.Text = task.RemindTime.Value.ToString("HH:mm");
                }

                // 加载额外提醒时间
                if (task.AdditionalRemindTimes != null && task.AdditionalRemindTimes.Count > 0)
                {
                    foreach (var rt in task.AdditionalRemindTimes)
                    {
                        _additionalReminders.Add(new AdditionalReminderItem(rt));
                    }
                    UpdateAdditionalRemindersVisibility();
                }

                // 加载目标日期
                ChkHasTargetDate.IsChecked = task.TargetDate.HasValue;
                if (task.TargetDate.HasValue)
                {
                    DpTargetDate.SelectedDate = task.TargetDate.Value.Date;
                }

                CmbPriority.SelectedIndex = (int)task.Priority;

                _isLoadingSettings = true;
                LoadTaskRepeatSettings(task);
                _isLoadingSettings = false;
            }
            else
            {
                this.Title = "添加任务";
            }
        }

        private void InitializeCombos()
        {
            CmbMonthlyDay.Items.Clear();
            for (int i = 1; i <= 31; i++)
            {
                var item = new ComboBoxItem { Content = $"{i}号", Tag = i };
                CmbMonthlyDay.Items.Add(item);
            }
            CmbMonthlyDay.SelectedIndex = 0;

            CmbQuarterlyDay.Items.Clear();
            for (int i = 1; i <= 31; i++)
            {
                var item = new ComboBoxItem { Content = $"{i}号", Tag = i };
                CmbQuarterlyDay.Items.Add(item);
            }
            CmbQuarterlyDay.SelectedIndex = 0;

            CmbYearlyDay.Items.Clear();
            for (int i = 1; i <= 31; i++)
            {
                var item = new ComboBoxItem { Content = $"{i}号", Tag = i };
                CmbYearlyDay.Items.Add(item);
            }
            CmbYearlyDay.SelectedIndex = 0;
        }

        private void ChkHasRemind_Changed(object sender, RoutedEventArgs e)
        {
            if (DpRemindDate == null) return;
            bool isEnabled = ChkHasRemind.IsChecked == true;
            ReminderPanel.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ChkHasTargetDate_Changed(object sender, RoutedEventArgs e)
        {
            if (DpTargetDate == null) return;
            bool isEnabled = ChkHasTargetDate.IsChecked == true;
            TargetDatePanel.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnAddReminder_Click(object sender, RoutedEventArgs e)
        {
            var defaultDate = DpRemindDate.SelectedDate ?? DateTime.Now;
            var defaultTime = TxtRemindTime.Text;
            if (!TimeSpan.TryParse(defaultTime, out _))
                defaultTime = "09:00";

            _additionalReminders.Add(new AdditionalReminderItem(defaultDate, defaultTime));
            UpdateAdditionalRemindersVisibility();
        }

        private void BtnRemoveReminder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AdditionalReminderItem item)
            {
                _additionalReminders.Remove(item);
                UpdateAdditionalRemindersVisibility();
            }
        }

        private void AdditionalReminderDate_Changed(object sender, SelectionChangedEventArgs e)
        {
            // DatePicker binding handles it via INotifyPropertyChanged
        }

        private void AdditionalReminderTime_Changed(object sender, TextChangedEventArgs e)
        {
            // TextBox binding handles it via INotifyPropertyChanged
        }

        private void UpdateAdditionalRemindersVisibility()
        {
            AdditionalRemindersPanel.Visibility = _additionalReminders.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadTaskRepeatSettings(TaskItem task)
        {
            if (task.RepeatDetail == null) return;

            CmbRepeat.SelectedIndex = (int)task.RepeatDetail.RepeatType;

            if (task.RepeatDetail.RepeatInterval.HasValue)
            {
                TxtDailyInterval.Text = task.RepeatDetail.RepeatInterval.Value.ToString();
            }

            if (task.RepeatDetail.WeeklyDays != null)
            {
                LoadWeekdays(task.RepeatDetail.WeeklyDays);
            }
            else if (task.RepeatDetail.WeeklyDay.HasValue)
            {
                LoadSingleWeekday(task.RepeatDetail.WeeklyDay.Value);
            }

            if (task.RepeatDetail.MonthlyDay.HasValue)
            {
                RbMonthlyDay.IsChecked = true;
                SelectDay(CmbMonthlyDay, task.RepeatDetail.MonthlyDay.Value);
            }
            else if (task.RepeatDetail.MonthlyWeekNumber.HasValue && task.RepeatDetail.MonthlyWeekDay.HasValue)
            {
                RbMonthlyWeek.IsChecked = true;
                SelectWeekNumber(task.RepeatDetail.MonthlyWeekNumber.Value);
                SelectWeekday(CmbMonthlyWeekDay, task.RepeatDetail.MonthlyWeekDay.Value);
            }
            else if (task.RepeatDetail.IsMonthlyLastDay)
            {
                RbMonthlyLastDay.IsChecked = true;
            }

            if (task.RepeatDetail.YearlyMonth.HasValue && task.RepeatDetail.YearlyDay.HasValue)
            {
                SelectMonth(task.RepeatDetail.YearlyMonth.Value);
                SelectDay(CmbYearlyDay, task.RepeatDetail.YearlyDay.Value);
            }

            if (task.RepeatDetail.QuarterlyMonth.HasValue && task.RepeatDetail.QuarterlyDay.HasValue)
            {
                SelectQuarterlyMonth(task.RepeatDetail.QuarterlyMonth.Value);
                SelectDay(CmbQuarterlyDay, task.RepeatDetail.QuarterlyDay.Value);
            }

            if (task.RepeatDetail.EndAfterOccurrences.HasValue)
            {
                RbEndAfter.IsChecked = true;
                TxtEndAfterCount.Text = task.RepeatDetail.EndAfterOccurrences.Value.ToString();
            }
            else if (task.RepeatDetail.EndDate.HasValue)
            {
                RbEndOnDate.IsChecked = true;
                DpEndDate.SelectedDate = task.RepeatDetail.EndDate.Value;
            }
        }

        private void LoadWeekdays(List<DayOfWeek> days)
        {
            foreach (var day in days) SetWeekdayCheckBox(day, true);
        }

        private void LoadSingleWeekday(DayOfWeek day)
        {
            SetWeekdayCheckBox(day, true);
        }

        private void SetWeekdayCheckBox(DayOfWeek day, bool isChecked)
        {
            var checkBox = GetWeekdayCheckBox(day);
            if (checkBox != null) checkBox.IsChecked = isChecked;
        }

        private CheckBox? GetWeekdayCheckBox(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => ChkMon,
                DayOfWeek.Tuesday => ChkTue,
                DayOfWeek.Wednesday => ChkWed,
                DayOfWeek.Thursday => ChkThu,
                DayOfWeek.Friday => ChkFri,
                DayOfWeek.Saturday => ChkSat,
                DayOfWeek.Sunday => ChkSun,
                _ => null
            };
        }

        private void SelectDay(ComboBox cmb, int day)
        {
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                var item = cmb.Items[i] as ComboBoxItem;
                if (item != null && int.TryParse(item.Tag.ToString(), out int tag) && tag == day)
                { cmb.SelectedIndex = i; return; }
            }
            cmb.SelectedIndex = 0;
        }

        private void SelectWeekNumber(int weekNumber)
        {
            for (int i = 0; i < CmbMonthlyWeekNum.Items.Count; i++)
            {
                var item = CmbMonthlyWeekNum.Items[i] as ComboBoxItem;
                if (item != null && int.TryParse(item.Tag.ToString(), out int tag) && tag == weekNumber)
                { CmbMonthlyWeekNum.SelectedIndex = i; return; }
            }
            CmbMonthlyWeekNum.SelectedIndex = 0;
        }

        private void SelectWeekday(ComboBox cmb, DayOfWeek day)
        {
            int tag = (int)day;
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                var item = cmb.Items[i] as ComboBoxItem;
                if (item != null && int.TryParse(item.Tag.ToString(), out int itemTag) && itemTag == tag)
                { cmb.SelectedIndex = i; return; }
            }
            cmb.SelectedIndex = 0;
        }

        private void SelectMonth(int month)
        {
            for (int i = 0; i < CmbYearlyMonth.Items.Count; i++)
            {
                var item = CmbYearlyMonth.Items[i] as ComboBoxItem;
                if (item != null && int.TryParse(item.Tag.ToString(), out int tag) && tag == month)
                { CmbYearlyMonth.SelectedIndex = i; return; }
            }
            CmbYearlyMonth.SelectedIndex = 0;
        }

        private void SelectQuarterlyMonth(int month)
        {
            for (int i = 0; i < CmbQuarterlyMonth.Items.Count; i++)
            {
                var item = CmbQuarterlyMonth.Items[i] as ComboBoxItem;
                if (item != null && int.TryParse(item.Tag.ToString(), out int tag) && tag == month)
                { CmbQuarterlyMonth.SelectedIndex = i; return; }
            }
            CmbQuarterlyMonth.SelectedIndex = 0;
        }

        private void CmbRepeat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            if (DailyPanel == null) return;

            DailyPanel.Visibility = Visibility.Collapsed;
            WeeklyPanel.Visibility = Visibility.Collapsed;
            MonthlyPanel.Visibility = Visibility.Collapsed;
            QuarterlyPanel.Visibility = Visibility.Collapsed;
            YearlyPanel.Visibility = Visibility.Collapsed;

            if (CmbRepeat.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                var tagStr = item.Tag.ToString();
                if (int.TryParse(tagStr, out int tag))
                {
                    switch (tag)
                    {
                        case 1: DailyPanel.Visibility = Visibility.Visible; break;
                        case 2: WeeklyPanel.Visibility = Visibility.Visible; break;
                        case 3: MonthlyPanel.Visibility = Visibility.Visible; break;
                        case 4: QuarterlyPanel.Visibility = Visibility.Visible; break;
                        case 5: YearlyPanel.Visibility = Visibility.Visible; break;
                        case 6: SetWorkdays(); break;
                    }
                }
            }
        }

        private void SetWorkdays()
        {
            ChkMon.IsChecked = true; ChkTue.IsChecked = true; ChkWed.IsChecked = true;
            ChkThu.IsChecked = true; ChkFri.IsChecked = true;
            ChkSat.IsChecked = false; ChkSun.IsChecked = false;
        }

        private void RbMonthly_Checked(object sender, RoutedEventArgs e)
        {
            CmbMonthlyDay.IsEnabled = RbMonthlyDay.IsChecked == true;
            CmbMonthlyWeekNum.IsEnabled = RbMonthlyWeek.IsChecked == true;
            CmbMonthlyWeekDay.IsEnabled = RbMonthlyWeek.IsChecked == true;
        }

        private void RbEnd_Checked(object sender, RoutedEventArgs e)
        {
            TxtEndAfterCount.IsEnabled = RbEndAfter.IsChecked == true;
            DpEndDate.IsEnabled = RbEndOnDate.IsChecked == true;
        }

        private void RbEnd_Unchecked(object sender, RoutedEventArgs e)
        {
            if (RbEndAfter.IsChecked != true) TxtEndAfterCount.IsEnabled = false;
            if (RbEndOnDate.IsChecked != true) DpEndDate.IsEnabled = false;
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            var rawText = TxtContent.Text.Trim();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                MessageBox.Show("请输入任务内容！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            TaskContent = lines[0].Trim();
            TaskSubTasks = new List<SubTaskItem>();

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    bool isChecked = line.StartsWith("☑ ");
                    var text = isChecked ? line.Substring(2) : line;
                    if (!isChecked && _originalSubTasks != null)
                    {
                        var original = _originalSubTasks.FirstOrDefault(s => s.Text == text);
                        if (original != null) isChecked = original.IsChecked;
                    }
                    TaskSubTasks.Add(new SubTaskItem { Text = text, IsChecked = isChecked });
                }
            }

            if (ChkHasRemind.IsChecked == true)
            {
                if (!DpRemindDate.SelectedDate.HasValue)
                {
                    MessageBox.Show("请选择提醒日期！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (TimeSpan.TryParse(TxtRemindTime.Text, out var time))
                {
                    TaskRemindTime = DpRemindDate.SelectedDate.Value.Date + time;
                }
                else
                {
                    MessageBox.Show("时间格式不正确，请使用 HH:mm 格式！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // 收集额外提醒时间
            TaskAdditionalRemindTimes = new List<DateTime>();
            foreach (var item in _additionalReminders)
            {
                if (item.Date.HasValue && TimeSpan.TryParse(item.TimeStr, out var t))
                {
                    TaskAdditionalRemindTimes.Add(item.Date.Value.Date + t);
                }
            }

            // 收集目标日期
            if (ChkHasTargetDate.IsChecked == true && DpTargetDate.SelectedDate.HasValue)
            {
                TaskTargetDate = DpTargetDate.SelectedDate.Value.Date;
            }

            if (CmbPriority.SelectedItem is ComboBoxItem priorityItem && priorityItem.Tag != null)
            {
                if (int.TryParse(priorityItem.Tag.ToString(), out int priorityValue))
                    TaskPriority = (PriorityLevel)priorityValue;
            }

            TaskRepeatDetail = new RepeatRuleDetail();

            if (CmbRepeat.SelectedItem is ComboBoxItem repeatItem && repeatItem.Tag != null)
            {
                if (int.TryParse(repeatItem.Tag.ToString(), out int repeatValue))
                {
                    TaskRepeat = (RepeatType)repeatValue;
                    TaskRepeatDetail.RepeatType = TaskRepeat;
                }
            }

            CollectRepeatDetails();

            this.DialogResult = true;
            this.Close();
        }

        private void CollectRepeatDetails()
        {
            if (TaskRepeat == RepeatType.Daily)
            {
                if (int.TryParse(TxtDailyInterval.Text, out int interval))
                    TaskRepeatDetail.RepeatInterval = interval;
            }
            else if (TaskRepeat == RepeatType.Weekly || TaskRepeat == RepeatType.Workdays)
            {
                var selectedDays = new List<DayOfWeek>();
                if (ChkMon.IsChecked == true) selectedDays.Add(DayOfWeek.Monday);
                if (ChkTue.IsChecked == true) selectedDays.Add(DayOfWeek.Tuesday);
                if (ChkWed.IsChecked == true) selectedDays.Add(DayOfWeek.Wednesday);
                if (ChkThu.IsChecked == true) selectedDays.Add(DayOfWeek.Thursday);
                if (ChkFri.IsChecked == true) selectedDays.Add(DayOfWeek.Friday);
                if (ChkSat.IsChecked == true) selectedDays.Add(DayOfWeek.Saturday);
                if (ChkSun.IsChecked == true) selectedDays.Add(DayOfWeek.Sunday);
                if (selectedDays.Count > 0) TaskRepeatDetail.WeeklyDays = selectedDays;
            }
            else if (TaskRepeat == RepeatType.Monthly)
            {
                if (RbMonthlyDay.IsChecked == true)
                {
                    if (CmbMonthlyDay.SelectedItem is ComboBoxItem dayItem && dayItem.Tag != null)
                        if (int.TryParse(dayItem.Tag.ToString(), out int dayValue))
                            TaskRepeatDetail.MonthlyDay = dayValue;
                }
                else if (RbMonthlyWeek.IsChecked == true)
                {
                    if (CmbMonthlyWeekNum.SelectedItem is ComboBoxItem weekNumItem && weekNumItem.Tag != null)
                        if (int.TryParse(weekNumItem.Tag.ToString(), out int weekNumValue))
                            TaskRepeatDetail.MonthlyWeekNumber = weekNumValue;
                    if (CmbMonthlyWeekDay.SelectedItem is ComboBoxItem weekDayItem && weekDayItem.Tag != null)
                        if (int.TryParse(weekDayItem.Tag.ToString(), out int weekDayValue))
                            TaskRepeatDetail.MonthlyWeekDay = (DayOfWeek)weekDayValue;
                }
                else if (RbMonthlyLastDay.IsChecked == true)
                    TaskRepeatDetail.IsMonthlyLastDay = true;
            }
            else if (TaskRepeat == RepeatType.Quarterly)
            {
                if (CmbQuarterlyMonth.SelectedItem is ComboBoxItem monthItem && monthItem.Tag != null)
                    if (int.TryParse(monthItem.Tag.ToString(), out int monthValue))
                        TaskRepeatDetail.QuarterlyMonth = monthValue;
                if (CmbQuarterlyDay.SelectedItem is ComboBoxItem dayItem && dayItem.Tag != null)
                    if (int.TryParse(dayItem.Tag.ToString(), out int dayValue))
                        TaskRepeatDetail.QuarterlyDay = dayValue;
            }
            else if (TaskRepeat == RepeatType.Yearly)
            {
                if (CmbYearlyMonth.SelectedItem is ComboBoxItem monthItem && monthItem.Tag != null)
                    if (int.TryParse(monthItem.Tag.ToString(), out int monthValue))
                        TaskRepeatDetail.YearlyMonth = monthValue;
                if (CmbYearlyDay.SelectedItem is ComboBoxItem dayItem && dayItem.Tag != null)
                    if (int.TryParse(dayItem.Tag.ToString(), out int dayValue))
                        TaskRepeatDetail.YearlyDay = dayValue;
            }

            if (RbEndAfter.IsChecked == true)
            {
                if (int.TryParse(TxtEndAfterCount.Text, out int count))
                    TaskRepeatDetail.EndAfterOccurrences = count;
            }
            else if (RbEndOnDate.IsChecked == true)
            {
                if (DpEndDate.SelectedDate.HasValue)
                    TaskRepeatDetail.EndDate = DpEndDate.SelectedDate.Value;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
                button.Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
                button.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
        }

        private void BtnOK_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
                button.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x66, 0xCC));
        }

        private void BtnOK_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
                button.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xFF));
        }
    }

    /// <summary>
    /// 额外提醒时间的展示模型
    /// </summary>
    public class AdditionalReminderItem : INotifyPropertyChanged
    {
        private DateTime? _date;
        private string _timeStr;

        public DateTime? Date
        {
            get => _date;
            set { _date = value; OnPropertyChanged(); OnPropertyChanged(nameof(RemindTimeStr)); }
        }

        public string TimeStr
        {
            get => _timeStr;
            set { _timeStr = value; OnPropertyChanged(); OnPropertyChanged(nameof(RemindTimeStr)); }
        }

        public string RemindTimeStr
        {
            get
            {
                if (Date.HasValue && TimeSpan.TryParse(TimeStr, out var t))
                    return $"{Date.Value:yyyy-MM-dd} {TimeStr}";
                return "未设置";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public AdditionalReminderItem(DateTime? date = null, string timeStr = "09:00")
        {
            _date = date;
            _timeStr = timeStr;
        }

        public AdditionalReminderItem(DateTime dateTime)
        {
            _date = dateTime.Date;
            _timeStr = dateTime.ToString("HH:mm");
        }
    }
}
