using System;
using System.Collections.Generic;

namespace DesktopToDo.Models
{
    public class RepeatRuleDetail
    {
        public RepeatType RepeatType { get; set; } = RepeatType.None;
        
        public int? RepeatInterval { get; set; }
        
        public DayOfWeek? WeeklyDay { get; set; }
        
        public List<DayOfWeek>? WeeklyDays { get; set; }
        
        public int? MonthlyDay { get; set; }
        
        public int? MonthlyWeekNumber { get; set; }
        
        public DayOfWeek? MonthlyWeekDay { get; set; }
        
        public bool IsMonthlyLastDay { get; set; }
        
        public int? QuarterlyMonth { get; set; }
        
        public int? QuarterlyDay { get; set; }
        
        public int? YearlyMonth { get; set; }
        
        public int? YearlyDay { get; set; }
        
        public int? EndAfterOccurrences { get; set; }
        
        public DateTime? EndDate { get; set; }
    }
}