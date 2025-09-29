using System.Collections.Generic;
using System.Linq;
using DICOMAnon.Exporter.Models;
using DAAPI.Models;

namespace DICOMAnon.Exporter.Services
{
    public class WorklistBuilder : IWorklistBuilder
    {
        public WorklistItem Build(SelectionResult selection)
        {
            var wl = new WorklistItem
            {
                Instances = new List<WorklistInstance>(),
                PatientId = selection.PatientId
            };

            foreach (var s in selection.SeriesUids)
            {
                wl.Instances.Add(new WorklistInstance { SeriesInstanceUID = s });
            }

            foreach (var inst in selection.Instances)
            {
                wl.Instances.Add(new WorklistInstance
                {
                    SeriesInstanceUID = inst.SeriesUid,
                    SOPInstanceUID = inst.InstanceUid
                });
            }

            // De-duplicate
            wl.Instances = wl.Instances
                .GroupBy(i => (i.SeriesInstanceUID, i.SOPInstanceUID))
                .Select(g => g.First())
                .ToList();

            return wl;
        }
    }
}

