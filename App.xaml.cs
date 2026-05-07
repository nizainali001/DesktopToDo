using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DesktopToDo
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                try
                {
                    var logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "DesktopToDo", "crash.log");
                    File.AppendAllText(logPath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UnhandledException: {args.ExceptionObject}\n");
                }
                catch { }
            };

            DispatcherUnhandledException += (s, args) =>
            {
                try
                {
                    var logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "DesktopToDo", "crash.log");
                    File.AppendAllText(logPath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] DispatcherException: {args.Exception}\n");
                }
                catch { }
                MessageBox.Show($"程序发生错误：\n\n{args.Exception.Message}\n\n错误详情已记录到 crash.log",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}
