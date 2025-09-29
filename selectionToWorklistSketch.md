After the user has selected the instances or series to export, we need to convert those into a certain type of object called a work list that will be sent through a web API to a server to initiate the export. Here is an example of how to convert series and the work list type. 

```cs
  private WorklistItem ConvertSelectionToWorklist()
  {
      var wl = new WorklistItem() { Instances = new System.Collections.Generic.List<WorklistInstance>() };
      wl.PatientId = Patient.FirstOrDefault()?.ID;
      var patient = Patient.FirstOrDefault();
      if (patient != null)
      {
          var allDescendents = patient.Descendents();
          foreach (var node in allDescendents.Where(d => d.IsSelected && d is IInstance))
          {
              var instance = node as IInstance;
              wl.Instances.Add(new WorklistInstance()
              {
                  SOPInstanceUID = instance.SOPInstanceUID,
                  SeriesInstanceUID = instance.SeriesUID
              });
          }
          foreach (var node in allDescendents.Where(d => d.IsSelected && d is ISeries))
          {
              var series = node as ISeries;
              wl.Instances.Add(new WorklistInstance()
              {
                  SeriesInstanceUID = series.SeriesUID
              });
          }
      }
      return wl;
  }
  ```

  A WorklistInstance is a class from a 3rd party library (referenced in this project) called DICOMAnon.API. It accepts an object with either a series instance UID and SOP instance UID or just the series instance UID. Worklist instances are children of the Worklist item class, which is from the same library. 

  Afterwards, we will need to use this object to actually perform the export through a series of API calls. We will use the DICOM client to send the web API call and then pull the response from the service. Here is a short example from a snippet I found:

  ```cs
  
namespace DICOMAnon.Exporter.Services
{
    public class DAClientSyncService
    {
        internal const int API_PORT = 13997;

        private DAClient _client;
        public DAClientSyncService(IAppSettingsService ss)
        {
            try
            {
                //Whenever settings change, we need to recreate the client:
                _client = new DAClient(ss.AppSettings.DICOMAnonIPAddress, API_PORT, ss.AppSettings.DICOMAnonAPIKey);
            }
            catch (System.Exception)
            {
            }
        }

        public Task<int> Export(WorklistItem wlItem, IdentityMapping idMap, bool isIdentityMappingAllowed)
        {
            return Task.Run(() =>
            {
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
```

And then
```cs
            ExportSelectionCommand = new ....
            {
                if (_currentWorklistItem != null)
                {
                    IsBusy = true;
                    var job = await _client.Export(_currentWorklistItem, _currentMapping, _isIdentityMappingAllowed);
                    await PollJob(job);
                    IsBusy = false;
                }
            });
```

The current mapping is just a setting that allows you to dynamically change the patient ID and patient name during the export process. It's a feature allowed by the web API service we are using. 