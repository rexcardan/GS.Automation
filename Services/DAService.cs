using DAAPI;
using DAAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DICOMAnon.Exporter.Services
{
    public class DAClientSyncService
    {
        private readonly IAppSettingsService _ss;
        private DAClient _client;
        public DAClientSyncService(IAppSettingsService ss)
        {
            _ss = ss;
            try
            {
                RecreateClient();
            }
            catch (System.Exception)
            {
            }
        }

        private void RecreateClient()
        {
            _client = new DAClient(_ss.AppSettings.DICOMAnonIPAddress, _ss.AppSettings.DICOMAnonPort, _ss.AppSettings.DICOMAnonAPIKey);
        }

        public bool TestConnection()
        {
            try
            {
                RecreateClient();
                return _client.IsClientConnected();
            }
            catch
            {
                return false;
            }
        }

        public Task<int> Export(WorklistItem wlItem, IdentityMapping idMap, bool isIdentityMappingAllowed)
        {
            return Task.Run(() =>
            {
                RecreateClient();
                _client.ClearWorklist();

                if (isIdentityMappingAllowed)
                {
                    wlItem.MapToId = idMap.NewId;
                    wlItem.MapToName = $"{idMap.NewLastName}^{idMap.NewFirstName}";
                }

                if (isIdentityMappingAllowed && !string.IsNullOrEmpty(idMap.NewId))
                {
                    var clientSettings = _client.GetSettings();
                    clientSettings.AnonymizationSettings.IdentityMappings.RemoveAll(i => i.OldId == idMap.OldId);
                    clientSettings.AnonymizationSettings.IdentityMappings.Add(idMap);
                    _client.UpdateSettings(clientSettings);
                }

                var jobId = _client.AddToWorklist(wlItem);
                var settings = _client.GetSettings();

                settings.AnonymizationSettings.IsModificationEnabled = false;
                settings.PostProcessors.Clear();
                _client.UpdateSettings(settings);
                _client.Run();
                return jobId;
            });
        }

        public Task<WorklistItemProgress> GetJobStatus(int job)
        {
            return Task.Run(() =>
            {
                return _client.GetItemStatus(job);
            });
        }
    }
}
