using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using DesktopToDo.Models;

namespace DesktopToDo.Services
{
    public class ReminderService
    {
        private readonly DispatcherTimer _timer;
        private readonly HashSet<string> _remindedKeys = new HashSet<string>();
        private List<TaskItem> _tasks = new List<TaskItem>();
        private int _advanceMinutes = 5;

        public event Action<TaskItem, DateTime>? OnRemind;

        public ReminderService()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _timer.Tick += CheckReminders;
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void UpdateTasks(List<TaskItem> tasks)
        {
            _tasks = tasks.Where(t => !t.IsCompleted && t.RemindTime.HasValue).ToList();
        }

        public void UpdateAdvanceMinutes(int minutes)
        {
            _advanceMinutes = minutes;
        }

        private void CheckReminders(object? sender, EventArgs e)
        {
            var now = DateTime.Now;

            foreach (var task in _tasks)
            {
                // 检查主提醒时间
                CheckSingleRemindTime(task, task.RemindTime, now, "main");

                // 检查额外提醒时间
                if (task.AdditionalRemindTimes != null)
                {
                    for (int i = 0; i < task.AdditionalRemindTimes.Count; i++)
                    {
                        CheckSingleRemindTime(task, task.AdditionalRemindTimes[i], now, $"add_{i}");
                    }
                }
            }
        }

        private void CheckSingleRemindTime(TaskItem task, DateTime? remindTime, DateTime now, string keySuffix)
        {
            if (!remindTime.HasValue) return;

            var key = $"{task.TaskId}_{keySuffix}";
            if (_remindedKeys.Contains(key)) return;

            var rt = remindTime.Value;
            var advanceTime = rt.AddMinutes(-_advanceMinutes);

            if (now >= advanceTime && now < rt.AddMinutes(1))
            {
                _remindedKeys.Add(key);
                OnRemind?.Invoke(task, rt);
            }
        }

        public void MarkReminded(Guid taskId)
        {
            _remindedKeys.Add($"{taskId}_main");
        }

        public void ClearReminded(Guid taskId)
        {
            var keysToRemove = _remindedKeys.Where(k => k.StartsWith($"{taskId}_")).ToList();
            foreach (var key in keysToRemove)
            {
                _remindedKeys.Remove(key);
            }
        }
    }
}
