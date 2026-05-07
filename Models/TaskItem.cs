using System;
using System.Collections.Generic;

namespace DesktopToDo.Models
{
    public class TaskItem
    {
        public Guid TaskId { get; set; }
        public string Content { get; set; } = string.Empty;
        public List<SubTaskItem> SubTasks { get; set; } = new List<SubTaskItem>();
        public DateTime? RemindTime { get; set; }
        public PriorityLevel Priority { get; set; }
        public RepeatType Repeat { get; set; }
        public RepeatRuleDetail RepeatDetail { get; set; } = new RepeatRuleDetail();
        public DateTime CreatedTime { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedTime { get; set; }
    }
}
