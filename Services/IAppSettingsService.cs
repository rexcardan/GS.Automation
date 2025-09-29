using DICOMAnon.Exporter.Models;

namespace DICOMAnon.Exporter.Services
{
    public interface IAppSettingsService
    {
        AppSettings AppSettings { get; }
        string SettingsPath { get; }
        AppSettings Load();
        void Save(AppSettings settings);
        void Update(System.Action<AppSettings> updater);
    }
}

