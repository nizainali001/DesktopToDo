using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DesktopToDo.Models;

namespace DesktopToDo.Services
{
    public static class TaskService
    {
        public static TaskItem CreateTask(string content, DateTime? remindTime, PriorityLevel priority, RepeatType repeat, RepeatRuleDetail? repeatDetail = null)
        {
            return new TaskItem
            {
                TaskId = Guid.NewGuid(),
                Content = content,
                RemindTime = remindTime,
                Priority = priority,
                Repeat = repeat,
                RepeatDetail = repeatDetail ?? new RepeatRuleDetail(),
                CreatedTime = DateTime.Now,
                IsCompleted = false,
                CompletedTime = null
            };
        }

        public static List<TaskItem> GetPendingTasks(List<TaskItem> allTasks)
        {
            return allTasks.Where(t => !t.IsCompleted)
                          .OrderBy(t => t.RemindTime ?? DateTime.MaxValue)
                          .ThenBy(t => t.Priority, new PriorityComparer())
                          .ToList();
        }

        private class PriorityComparer : IComparer<PriorityLevel>
        {
            public int Compare(PriorityLevel x, PriorityLevel y)
            {
                // 排序优先级顺序：急(Urgent) > 重(High) > 轻(Low) > 缓(Normal)
                int GetOrder(PriorityLevel priority)
                {
                    return priority switch
                    {
                        PriorityLevel.Urgent => 0,
                        PriorityLevel.High => 1,
                        PriorityLevel.Low => 2,
                        PriorityLevel.Normal => 3,
                        _ => 4
                    };
                }
                return GetOrder(x).CompareTo(GetOrder(y));
            }
        }

        public static List<TaskItem> GetCompletedTasks(List<TaskItem> allTasks)
        {
            return allTasks.Where(t => t.IsCompleted)
                          .OrderByDescending(t => t.CompletedTime)
                          .ToList();
        }

        public static TaskItem? CompleteTask(TaskItem task, out TaskItem? newTask)
        {
            newTask = null;

            if (task.IsCompleted)
                return null;

            var completedTask = new TaskItem
            {
                TaskId = task.TaskId,
                Content = task.Content,
                RemindTime = task.RemindTime,
                Priority = task.Priority,
                Repeat = task.Repeat,
                RepeatDetail = task.RepeatDetail,
                CreatedTime = task.CreatedTime,
                IsCompleted = true,
                CompletedTime = DateTime.Now
            };

            return completedTask;
        }

        public static List<TaskItem> CheckAndRestoreRepeatingTasks(List<TaskItem> allTasks)
        {
            var newTasks = new List<TaskItem>();
            var pendingContents = allTasks.Where(t => !t.IsCompleted).Select(t => t.Content).ToHashSet();

            foreach (var completedTask in allTasks.Where(t => t.IsCompleted && t.Repeat != RepeatType.None))
            {
                if (pendingContents.Contains(completedTask.Content))
                    continue;

                var completionTime = completedTask.CompletedTime ?? completedTask.CreatedTime;
                if (!IsTimeToRestore(completionTime, completedTask.Repeat, completedTask.RepeatDetail))
                    continue;

                var newTask = new TaskItem
                {
                    TaskId = Guid.NewGuid(),
                    Content = completedTask.Content,
                    RemindTime = GetNextRemindTime(completionTime, completedTask.Repeat, completedTask.RepeatDetail, completedTask.RemindTime),
                    Priority = completedTask.Priority,
                    Repeat = completedTask.Repeat,
                    RepeatDetail = completedTask.RepeatDetail,
                    CreatedTime = DateTime.Now,
                    IsCompleted = false,
                    CompletedTime = null
                };
                newTasks.Add(newTask);
                pendingContents.Add(completedTask.Content);
            }

            return newTasks;
        }

        private static bool IsTimeToRestore(DateTime completionTime, RepeatType repeatType, RepeatRuleDetail? repeatDetail)
        {
            var now = DateTime.Now;
            return repeatType switch
            {
                RepeatType.Daily => IsTimeToRestoreDaily(completionTime, now, repeatDetail),
                RepeatType.Weekly => IsTimeToRestoreWeekly(completionTime, now, repeatDetail),
                RepeatType.Workdays => IsTimeToRestoreWorkdays(completionTime, now, repeatDetail),
                RepeatType.Monthly => IsTimeToRestoreMonthly(completionTime, now, repeatDetail),
                RepeatType.Quarterly => IsTimeToRestoreQuarterly(completionTime, now, repeatDetail),
                RepeatType.Yearly => IsTimeToRestoreYearly(completionTime, now, repeatDetail),
                _ => false
            };
        }

        private static bool IsTimeToRestoreDaily(DateTime completionTime, DateTime now, RepeatRuleDetail? detail)
        {
            var interval = detail?.RepeatInterval ?? 1;
            return (now - completionTime).TotalDays >= interval;
        }

        private static bool IsTimeToRestoreWeekly(DateTime completionTime, DateTime now, RepeatRuleDetail? detail)
        {
            if (detail?.WeeklyDays == null || detail.WeeklyDays.Count == 0)
                return GetIsoWeekNumber(completionTime) != GetIsoWeekNumber(now) || completionTime.Year != now.Year;
            
            foreach (var day in detail.WeeklyDays)
            {
                if (IsSameWeekdayAfterCompletion(completionTime, now, day))
                    return true;
            }
            return false;
        }

        private static bool IsTimeToRestoreWorkdays(DateTime completionTime, DateTime now, RepeatRuleDetail? detail)
        {
            var workdays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
            foreach (var day in workdays)
            {
                if (IsSameWeekdayAfterCompletion(completionTime, now, day))
                    return true;
            }
            return false;
        }

        private static bool IsSameWeekdayAfterCompletion(DateTime completionTime, DateTime now, DayOfWeek targetDay)
        {
            var nextTargetDay = GetNextWeekday(completionTime, targetDay);
            return now >= nextTargetDay;
        }

        private static DateTime GetNextWeekday(DateTime startDate, DayOfWeek targetDay)
        {
            int daysToAdd = ((int)targetDay - (int)startDate.DayOfWeek + 7) % 7;
            if (daysToAdd == 0) daysToAdd = 7;
            return startDate.AddDays(daysToAdd);
        }

        private static bool IsTimeToRestoreMonthly(DateTime completionTime, DateTime now, RepeatRuleDetail? detail)
        {
            if (detail == null)
                return completionTime.Year != now.Year || completionTime.Month != now.Month;

            if (detail.IsMonthlyLastDay)
                return IsLastDayOfMonthPassed(completionTime, now);
            else if (detail.MonthlyDay.HasValue)
                return IsSpecificDayOfMonthPassed(completionTime, now, detail.MonthlyDay.Value);
            else if (detail.MonthlyWeekNumber.HasValue && detail.MonthlyWeekDay.HasValue)
                return IsSpecificWeekOfMonthPassed(completionTime, now, detail.MonthlyWeekNumber.Value, detail.MonthlyWeekDay.Value);

            return completionTime.Year != now.Year || completionTime.Month != now.Month;
        }

        private static bool IsLastDayOfMonthPassed(DateTime completionTime, DateTime now)
        {
            if (completionTime.Year != now.Year || completionTime.Month != now.Month)
            {
                var nextMonth = completionTime.AddMonths(1);
                var lastDayNextMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                return now >= new DateTime(nextMonth.Year, nextMonth.Month, lastDayNextMonth).Date;
            }
            return false;
        }

        private static bool IsSpecificDayOfMonthPassed(DateTime completionTime, DateTime now, int targetDay)
        {
            if (completionTime.Year != now.Year || completionTime.Month != now.Month)
            {
                var nextMonth = completionTime.AddMonths(1);
                var daysInMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                var day = Math.Min(targetDay, daysInMonth);
                return now >= new DateTime(nextMonth.Year, nextMonth.Month, day).Date;
            }
            return false;
        }

        private static bool IsSpecificWeekOfMonthPassed(DateTime completionTime, DateTime now, int weekNumber, DayOfWeek weekday)
        {
            var nextMonth = completionTime.AddMonths(1);
            var targetDate = GetNthWeekdayOfMonth(nextMonth.Year, nextMonth.Month, weekNumber, weekday);
            return now >= targetDate.Date;
        }

        private static DateTime GetNthWeekdayOfMonth(int year, int month, int n, DayOfWeek weekday)
        {
            if (n == 5)
            {
                var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
                var daysToSubtract = ((int)lastDay.DayOfWeek - (int)weekday + 7) % 7;
                return lastDay.AddDays(-daysToSubtract);
            }

            var firstDay = new DateTime(year, month, 1);
            var daysToAdd = ((int)weekday - (int)firstDay.DayOfWeek + 7) % 7;
            var firstOccurrence = firstDay.AddDays(daysToAdd);
            var result = firstOccurrence.AddDays((n - 1) * 7);
            
            if (result.Month != month)
                return firstOccurrence.AddDays((n - 2) * 7);
            
            return result;
        }

        private static bool IsTimeToRestoreYearly(DateTime completionTime, DateTime now, RepeatRuleDetail? detail)
        {
            if (detail?.YearlyMonth.HasValue == true && detail.YearlyDay.HasValue)
            {
                var nextYearDate = new DateTime(completionTime.Year + 1, detail.YearlyMonth.Value, 
                    Math.Min(detail.YearlyDay.Value, DateTime.DaysInMonth(completionTime.Year + 1, detail.YearlyMonth.Value)));
                return now >= nextYearDate;
            }
            return completionTime.Year != now.Year;
        }

        private static bool IsTimeToRestoreQuarterly(DateTime completionTime, DateTime now, RepeatRuleDetail? detail)
        {
            if (detail?.QuarterlyMonth.HasValue == true && detail.QuarterlyDay.HasValue)
            {
                var nextQuarterDate = GetNextQuarterDate(completionTime, detail.QuarterlyMonth.Value, detail.QuarterlyDay.Value);
                return now >= nextQuarterDate;
            }
            return GetQuarter(completionTime) != GetQuarter(now) || completionTime.Year != now.Year;
        }

        private static int GetQuarter(DateTime date) => (date.Month - 1) / 3 + 1;

        private static DateTime GetNextQuarterDate(DateTime completionTime, int quarterMonth, int day)
        {
            var currentQuarter = GetQuarter(completionTime);
            var currentQuarterStartMonth = (currentQuarter - 1) * 3 + 1;
            var targetMonthInQuarter = currentQuarterStartMonth + quarterMonth - 1;
            
            DateTime targetDate;
            if (completionTime.Month < targetMonthInQuarter || 
                (completionTime.Month == targetMonthInQuarter && completionTime.Day < day))
            {
                targetDate = new DateTime(completionTime.Year, targetMonthInQuarter, 
                    Math.Min(day, DateTime.DaysInMonth(completionTime.Year, targetMonthInQuarter)));
            }
            else
            {
                var nextQuarter = currentQuarter % 4 + 1;
                var nextQuarterStartMonth = (nextQuarter - 1) * 3 + 1;
                var nextTargetMonthInQuarter = nextQuarterStartMonth + quarterMonth - 1;
                var year = nextQuarter == 1 ? completionTime.Year + 1 : completionTime.Year;
                targetDate = new DateTime(year, nextTargetMonthInQuarter, 
                    Math.Min(day, DateTime.DaysInMonth(year, nextTargetMonthInQuarter)));
            }
            
            return targetDate;
        }

        private static int GetIsoWeekNumber(DateTime date)
        {
            return CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        private static DateTime? GetNextRemindTime(DateTime completionTime, RepeatType repeatType, RepeatRuleDetail? repeatDetail, DateTime? originalRemindTime)
        {
            if (!originalRemindTime.HasValue)
                return null;

            var timeOfDay = originalRemindTime.Value.TimeOfDay;
            var nextDate = repeatType switch
            {
                RepeatType.Daily => GetNextDailyDate(completionTime, repeatDetail),
                RepeatType.Weekly => GetNextWeeklyDate(completionTime, repeatDetail),
                RepeatType.Workdays => GetNextWorkdayDate(completionTime, repeatDetail),
                RepeatType.Monthly => GetNextMonthlyDate(completionTime, repeatDetail),
                RepeatType.Quarterly => GetNextQuarterlyDate(completionTime, repeatDetail, originalRemindTime),
                RepeatType.Yearly => GetNextYearlyDate(completionTime, repeatDetail, originalRemindTime),
                _ => completionTime
            };
            
            return nextDate.Date + timeOfDay;
        }

        private static DateTime GetNextDailyDate(DateTime completionTime, RepeatRuleDetail? repeatDetail)
        {
            var interval = repeatDetail?.RepeatInterval ?? 1;
            return completionTime.AddDays(interval);
        }

        private static DateTime GetNextWeeklyDate(DateTime completionTime, RepeatRuleDetail? repeatDetail)
        {
            if (repeatDetail?.WeeklyDays == null || repeatDetail.WeeklyDays.Count == 0)
                return completionTime.AddDays(7);

            var nextDates = repeatDetail.WeeklyDays
                .Select(day => GetNextWeekday(completionTime, day))
                .OrderBy(d => d)
                .ToList();

            return nextDates.Count > 0 ? nextDates[0] : completionTime.AddDays(7);
        }

        private static DateTime GetNextWorkdayDate(DateTime completionTime, RepeatRuleDetail? repeatDetail)
        {
            var workdays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
            var nextDates = workdays
                .Select(day => GetNextWeekday(completionTime, day))
                .OrderBy(d => d)
                .ToList();

            return nextDates.Count > 0 ? nextDates[0] : completionTime.AddDays(7);
        }

        private static DateTime GetNextMonthlyDate(DateTime completionTime, RepeatRuleDetail? repeatDetail)
        {
            if (repeatDetail == null)
                return completionTime.AddMonths(1);

            if (repeatDetail.IsMonthlyLastDay)
                return GetLastDayOfNextMonth(completionTime);
            else if (repeatDetail.MonthlyDay.HasValue)
                return GetSpecificDayOfNextMonth(completionTime, repeatDetail.MonthlyDay.Value);
            else if (repeatDetail.MonthlyWeekNumber.HasValue && repeatDetail.MonthlyWeekDay.HasValue)
                return GetNthWeekdayOfNextMonth(completionTime, repeatDetail.MonthlyWeekNumber.Value, repeatDetail.MonthlyWeekDay.Value);

            return completionTime.AddMonths(1);
        }

        private static DateTime GetLastDayOfNextMonth(DateTime date)
        {
            var nextMonth = date.AddMonths(1);
            return new DateTime(nextMonth.Year, nextMonth.Month, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
        }

        private static DateTime GetSpecificDayOfNextMonth(DateTime date, int targetDay)
        {
            var nextMonth = date.AddMonths(1);
            var daysInMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
            var day = Math.Min(targetDay, daysInMonth);
            return new DateTime(nextMonth.Year, nextMonth.Month, day);
        }

        private static DateTime GetNthWeekdayOfNextMonth(DateTime date, int weekNumber, DayOfWeek weekday)
        {
            var nextMonth = date.AddMonths(1);
            return GetNthWeekdayOfMonth(nextMonth.Year, nextMonth.Month, weekNumber, weekday);
        }

        private static DateTime GetNextQuarterlyDate(DateTime completionTime, RepeatRuleDetail? repeatDetail, DateTime? originalRemindTime)
        {
            if (repeatDetail?.QuarterlyMonth.HasValue == true && repeatDetail.QuarterlyDay.HasValue)
            {
                return GetNextQuarterDate(completionTime, repeatDetail.QuarterlyMonth.Value, repeatDetail.QuarterlyDay.Value);
            }
            else if (originalRemindTime.HasValue)
            {
                var currentQuarter = GetQuarter(completionTime);
                var nextQuarter = currentQuarter % 4 + 1;
                var nextQuarterStartMonth = (nextQuarter - 1) * 3 + 1;
                var quarterMonth = originalRemindTime.Value.Month - ((currentQuarter - 1) * 3);
                quarterMonth = Math.Max(1, Math.Min(3, quarterMonth));
                var nextTargetMonth = nextQuarterStartMonth + quarterMonth - 1;
                var year = nextQuarter == 1 ? completionTime.Year + 1 : completionTime.Year;
                return new DateTime(year, nextTargetMonth, 
                    Math.Min(originalRemindTime.Value.Day, DateTime.DaysInMonth(year, nextTargetMonth)));
            }
            return completionTime.AddMonths(3);
        }

        private static DateTime GetNextYearlyDate(DateTime completionTime, RepeatRuleDetail? repeatDetail, DateTime? originalRemindTime)
        {
            if (repeatDetail?.YearlyMonth.HasValue == true && repeatDetail.YearlyDay.HasValue)
            {
                var nextYear = completionTime.Year + 1;
                var daysInMonth = DateTime.DaysInMonth(nextYear, repeatDetail.YearlyMonth.Value);
                var day = Math.Min(repeatDetail.YearlyDay.Value, daysInMonth);
                return new DateTime(nextYear, repeatDetail.YearlyMonth.Value, day);
            }
            else if (originalRemindTime.HasValue)
            {
                return new DateTime(completionTime.Year + 1, originalRemindTime.Value.Month, 
                    Math.Min(originalRemindTime.Value.Day, DateTime.DaysInMonth(completionTime.Year + 1, originalRemindTime.Value.Month)));
            }
            return completionTime.AddYears(1);
        }

        public static string GetPriorityColor(PriorityLevel priority)
        {
            return priority switch
            {
                PriorityLevel.Low => "#0066FF",
                PriorityLevel.Normal => "#33CC33",
                PriorityLevel.High => "#FFD700",
                PriorityLevel.Urgent => "#FF3333",
                _ => "#33CC33"
            };
        }

        public static string GetPriorityText(PriorityLevel priority)
        {
            return priority switch
            {
                PriorityLevel.Low => "低",
                PriorityLevel.Normal => "普通",
                PriorityLevel.High => "高",
                PriorityLevel.Urgent => "紧急",
                _ => "普通"
            };
        }

        public static string GetRepeatShortLabel(RepeatType repeat)
        {
            return repeat switch
            {
                RepeatType.Daily => "日",
                RepeatType.Weekly => "周",
                RepeatType.Workdays => "工",
                RepeatType.Monthly => "月",
                RepeatType.Quarterly => "季",
                RepeatType.Yearly => "年",
                _ => ""
            };
        }

        public static string GetRepeatColor(RepeatType repeat, AppSettings settings)
        {
            return repeat switch
            {
                RepeatType.Daily => settings.DailyRepeatColor,
                RepeatType.Weekly => settings.WeeklyRepeatColor,
                RepeatType.Workdays => settings.WorkdaysRepeatColor,
                RepeatType.Monthly => settings.MonthlyRepeatColor,
                RepeatType.Quarterly => settings.QuarterlyRepeatColor,
                RepeatType.Yearly => settings.YearlyRepeatColor,
                _ => "#888888"
            };
        }

        public static string GetRepeatText(RepeatType repeat, RepeatRuleDetail detail)
        {
            return repeat switch
            {
                RepeatType.None => "无",
                RepeatType.Daily => GetDailyRepeatText(detail),
                RepeatType.Weekly => GetWeeklyRepeatText(detail),
                RepeatType.Workdays => "工作日",
                RepeatType.Monthly => GetMonthlyRepeatText(detail),
                RepeatType.Quarterly => GetQuarterlyRepeatText(detail),
                RepeatType.Yearly => GetYearlyRepeatText(detail),
                _ => "无"
            };
        }

        private static string GetDailyRepeatText(RepeatRuleDetail detail)
        {
            var interval = detail.RepeatInterval ?? 1;
            return interval == 1 ? "每天" : $"每{interval}天";
        }

        private static string GetWeeklyRepeatText(RepeatRuleDetail detail)
        {
            if (detail.WeeklyDays != null && detail.WeeklyDays.Count > 0)
            {
                var dayTexts = detail.WeeklyDays.Select(GetShortDayOfWeekText).ToList();
                return $"每周{string.Join("、", dayTexts)}";
            }
            else if (detail.WeeklyDay.HasValue)
            {
                return $"每周{GetShortDayOfWeekText(detail.WeeklyDay.Value)}";
            }
            return "每周";
        }

        private static string GetMonthlyRepeatText(RepeatRuleDetail detail)
        {
            if (detail.IsMonthlyLastDay)
                return "每月最后一天";
            else if (detail.MonthlyDay.HasValue)
                return $"每月{detail.MonthlyDay.Value}日";
            else if (detail.MonthlyWeekNumber.HasValue && detail.MonthlyWeekDay.HasValue)
            {
                var weekText = GetWeekNumberText(detail.MonthlyWeekNumber.Value);
                var dayText = GetShortDayOfWeekText(detail.MonthlyWeekDay.Value);
                return $"每月{weekText}{dayText}";
            }
            return "每月";
        }

        private static string GetYearlyRepeatText(RepeatRuleDetail detail)
        {
            if (detail.YearlyMonth.HasValue && detail.YearlyDay.HasValue)
            {
                return $"每年{detail.YearlyMonth.Value}月{detail.YearlyDay.Value}日";
            }
            return "每年";
        }

        private static string GetQuarterlyRepeatText(RepeatRuleDetail detail)
        {
            if (detail.QuarterlyMonth.HasValue && detail.QuarterlyDay.HasValue)
            {
                return $"每季度第{detail.QuarterlyMonth.Value}月{detail.QuarterlyDay.Value}日";
            }
            return "每季度";
        }

        private static string GetWeekNumberText(int weekNumber)
        {
            return weekNumber switch
            {
                1 => "第一个",
                2 => "第二个",
                3 => "第三个",
                4 => "第四个",
                5 => "最后一个",
                _ => "第一个"
            };
        }

        private static string GetShortDayOfWeekText(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Sunday => "日",
                DayOfWeek.Monday => "一",
                DayOfWeek.Tuesday => "二",
                DayOfWeek.Wednesday => "三",
                DayOfWeek.Thursday => "四",
                DayOfWeek.Friday => "五",
                DayOfWeek.Saturday => "六",
                _ => "一"
            };
        }
    }
}