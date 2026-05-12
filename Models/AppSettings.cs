using System.Collections.Generic;
using System.Windows;

namespace DesktopToDo.Models
{
    public class AppSettings
    {
        public double WindowOpacity { get; set; } = 0.9;
        public double BackgroundOpacity { get; set; } = 0.8;
        public double TextOpacity { get; set; } = 1.0;
        public string TextColor { get; set; } = "#F1F5F9"; // 默认浅色文字（深色主题）
        public string FontFamily { get; set; } = "Microsoft YaHei UI";
        public double FontSize { get; set; } = 14.0;
        public bool IsWallpaperMode { get; set; } = false;
        public bool IsAutoStart { get; set; } = false;
        public int AdvanceRemindMinutes { get; set; } = 5;
        public Point WindowPosition { get; set; } = new Point(100, 100);
        public Size WindowSize { get; set; } = new Size(500, 600);
        public ThemeColor ThemeColor { get; set; } = ThemeColor.Dark;
        public CustomColor CustomColor { get; set; } = new CustomColor { R = 144, G = 238, B = 144 };
        public bool IsEdgeHideEnabled { get; set; } = false;
        public string EdgeHideHotkey { get; set; } = "Ctrl+H";
        public TaskSortType TaskSortType { get; set; } = TaskSortType.Default;
        public bool ShowCreatedTime { get; set; } = false;
        public bool ShowRepeatLabel { get; set; } = false;
        public PinnedTaskNumberMode PinnedTaskNumberMode { get; set; } = PinnedTaskNumberMode.Unified;

        public string DailyRepeatColor { get; set; } = "#00FF00";
        public string WeeklyRepeatColor { get; set; } = "#00FF00";
        public string MonthlyRepeatColor { get; set; } = "#FF0000";
        public string QuarterlyRepeatColor { get; set; } = "#FF0000";
        public string YearlyRepeatColor { get; set; } = "#000000";
        public string WorkdaysRepeatColor { get; set; } = "#00FF00";

        // 序号圆点循环颜色（浅绿、蓝、黄、浅红、浅紫、粉）
        public List<string> TaskNumberColors { get; set; } = new List<string>
        {
            "#90EE90",  // 浅绿
            "#87CEEB",  // 浅蓝
            "#FBBF24",  // 黄色
            "#FF6B6B",  // 浅红
            "#DDA0DD",  // 浅紫
            "#FFB6C1"   // 粉色
        };
    }
}
