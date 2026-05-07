using System.Windows;

namespace DesktopToDo.Models
{
    public class AppSettings
    {
        public double WindowOpacity { get; set; } = 0.9;
        public string FontFamily { get; set; } = "Microsoft YaHei UI";
        public double FontSize { get; set; } = 14.0;
        public bool IsWallpaperMode { get; set; } = false;
        public bool IsAutoStart { get; set; } = false;
        public int AdvanceRemindMinutes { get; set; } = 5;
        public Point WindowPosition { get; set; } = new Point(100, 100);
        public Size WindowSize { get; set; } = new Size(500, 600);
        public ThemeColor ThemeColor { get; set; } = ThemeColor.LightGreen;
        public CustomColor CustomColor { get; set; } = new CustomColor { R = 144, G = 238, B = 144 };
        
        public string DailyRepeatColor { get; set; } = "#00FF00";
        public string WeeklyRepeatColor { get; set; } = "#00FF00";
        public string MonthlyRepeatColor { get; set; } = "#FF0000";
        public string QuarterlyRepeatColor { get; set; } = "#FF0000";
        public string YearlyRepeatColor { get; set; } = "#000000";
        public string WorkdaysRepeatColor { get; set; } = "#00FF00";
    }
}
