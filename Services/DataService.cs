using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DesktopToDo.Models;

namespace DesktopToDo.Services
{
    public static class DataService
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopToDo");

        private static readonly string TasksFilePath = Path.Combine(AppDataPath, "tasks.json");
        private static readonly string SettingsFilePath = Path.Combine(AppDataPath, "settings.json");

        static DataService()
        {
            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }
        }

        public static List<TaskItem> LoadTasks()
        {
            if (!File.Exists(TasksFilePath))
            {
                return new List<TaskItem>();
            }

            try
            {
                string json = File.ReadAllText(TasksFilePath);
                return JsonSerializer.Deserialize<List<TaskItem>>(json) ?? new List<TaskItem>();
            }
            catch
            {
                return new List<TaskItem>();
            }
        }

        public static void SaveTasks(List<TaskItem> tasks)
        {
            string json = JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(TasksFilePath, json);
        }

        public static AppSettings LoadSettings()
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            try
            {
                string json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void SaveSettings(AppSettings settings)
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }

        public static void ExportTasks(string filePath)
        {
            var tasks = LoadTasks();
            string json = JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        public static List<TaskItem> ImportTasks(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<TaskItem>>(json) ?? new List<TaskItem>();
        }
    }
}
