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
        private readonly HashSet<Guid> _remindedTasks = new HashSet<Guid>();
        private List<TaskItem> _tasks = new List<TaskItem>();
        private int _advanceMinutes = 5;

        public event Action<TaskItem>? OnRemind;

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
                if (!task.RemindTime.HasValue || _remindedTasks.Contains(task.TaskId))
                    continue;

                var remindTime = task.RemindTime.Value;
                var advanceTime = remindTime.AddMinutes(-_advanceMinutes);

                if (now >= advanceTime && now < remindTime.AddMinutes(1))
                {
                    _remindedTasks.Add(task.TaskId);
                    OnRemind?.Invoke(task);
                }
            }
        }

        public void MarkReminded(Guid taskId)
        {
            _remindedTasks.Add(taskId);
        }

        public void ClearReminded(Guid taskId)
        {
            _remindedTasks.Remove(taskId);
        }
    }
}
