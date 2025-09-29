using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using DICOMAnon.Exporter.Models;

namespace DICOMAnon.Exporter.Services
{
    public class AppSettingsService : IAppSettingsService
    {
        private readonly object _lock = new();
        public string SettingsPath { get; }
        public AppSettings AppSettings { get; private set; }

        public AppSettingsService(string settingsPath = null)
        {
            SettingsPath = string.IsNullOrWhiteSpace(settingsPath)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json")
                : settingsPath;
            AppSettings = Load();
        }

        public AppSettings Load()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(SettingsPath))
                    {
                        var json = File.ReadAllText(SettingsPath, Encoding.UTF8);
                        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
                        var ser = new DataContractJsonSerializer(typeof(AppSettings));
                        var loaded = (AppSettings)ser.ReadObject(ms);
                        // Migrate defaults if needed
                        if (loaded.DICOMAnonPort <= 0)
                            loaded.DICOMAnonPort = 13997;
                        if (loaded.Version == 0)
                            loaded.Version = 1;
                        AppSettings = loaded;
                        return AppSettings;
                    }
                }
                catch { /* ignore and fall back to defaults */ }

                AppSettings = new AppSettings();
                Save(AppSettings);
                return AppSettings;
            }
        }

        public void Save(AppSettings settings)
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath) ?? ".");
                using var ms = new MemoryStream();
                var ser = new DataContractJsonSerializer(typeof(AppSettings));
                ser.WriteObject(ms, settings);
                var json = Encoding.UTF8.GetString(ms.ToArray());
                File.WriteAllText(SettingsPath, json, Encoding.UTF8);
                AppSettings = settings;
            }
        }

        public void Update(Action<AppSettings> updater)
        {
            lock (_lock)
            {
                var s = AppSettings ?? new AppSettings();
                updater?.Invoke(s);
                Save(s);
            }
        }
    }
}

