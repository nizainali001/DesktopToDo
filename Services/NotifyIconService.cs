using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.IO;

namespace DesktopToDo.Services
{
    public static class NotifyIconService
    {
        public static NotifyIcon CreateNotifyIcon(string text, EventHandler doubleClickHandler, EventHandler showWindowHandler, EventHandler settingsHandler, EventHandler exitHandler)
        {
            var notifyIcon = new NotifyIcon();
            notifyIcon.Text = text;
            notifyIcon.Visible = true;

            try
            {
                notifyIcon.Icon = SystemIcons.Application;
            }
            catch
            {
            }

            var contextMenu = new ContextMenuStrip();
            var showItem = new ToolStripMenuItem("显示窗口");
            showItem.Click += showWindowHandler;
            var settingsItem = new ToolStripMenuItem("设置");
            settingsItem.Click += settingsHandler;
            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += exitHandler;
            contextMenu.Items.AddRange(new ToolStripItem[] { showItem, settingsItem, exitItem });
            notifyIcon.ContextMenuStrip = contextMenu;

            notifyIcon.DoubleClick += doubleClickHandler;

            return notifyIcon;
        }
    }
}
