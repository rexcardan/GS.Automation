using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace DICOMAnon.Exporter.Helpers
{
    public class TreeBuilder
    {
        public DicomTree BuildTree(VMS.TPS.Common.Model.API.Application app, string patientId)
        {
            var tree = new DicomTree();
            var pat = app.OpenPatientById(patientId);

            // Courses and Plans
            foreach (var course in pat.Courses)
            {
                var courseNode = new DicomNode
                {
                    Id = course.Id,
                    DisplayName = course.Id,
                    Type = DicomNodeType.Course
                };

                foreach (var planObj in course.PlanSetups)
                {
                    var planNode = new DicomNode
                    {
                        Id = planObj.Id,
                        DisplayName = planObj.Id,
                        Type = DicomNodeType.Plan
                    };

                    if (planObj.Dose != null)
                    {
                        planNode.Children.Add(new DicomNode
                        {
                            Id = planObj.Dose.UID,
                            DisplayName = $"Dose ({planObj.Id})",
                            Type = DicomNodeType.Dose,
                            SeriesUID = planObj.Dose.SeriesUID
                        });
                    }

                    if (planObj.StructureSet != null)
                    {
                        var ss = planObj.StructureSet;
                        planNode.Children.Add(new DicomNode
                        {
                            Id = ss.UID,
                            DisplayName = $"Structure Set ({planObj.Id})",
                            Type = DicomNodeType.StructureSet,
                            SeriesUID = ss.SeriesUID
                        });

                        if (ss.Image != null && ss.Image.Series != null)
                        {
                            planNode.Children.Add(new DicomNode
                            {
                                Id = ss.Image.Id,
                                DisplayName = $"Image ({planObj.Id})",
                                Type = DicomNodeType.Image,
                                SeriesUID = ss.Image.Series.UID
                            });
                        }
                    }

                    courseNode.Children.Add(planNode);
                }
                tree.Courses.Add(courseNode);
            }

            // Series and Instances
            foreach (var series in pat.Studies.SelectMany(s => s.Series))
            {
                var seriesNode = new DicomNode
                {
                    Id = series.UID,
                    DisplayName = BuildSeriesFriendlyName(pat, series),
                    Type = DicomNodeType.Series,
                    SeriesUID = series.UID
                };

                foreach (var image in series.Images)
                {
                    try
                    {
                        // Using image.Id as stand in for instance UID if not directly available
                        var instanceUid = image.UID ?? image.Id;
                        seriesNode.Children.Add(new DicomNode
                        {
                            Id = instanceUid,
                            DisplayName = image.Id,
                            Type = DicomNodeType.Instance,
                            SeriesUID = series.UID,
                            InstanceUID = instanceUid
                        });
                    }
                    catch { /* ignore malformed images */ }
                }
                tree.Series.Add(seriesNode);
            }

            return tree;
        }

        private static string BuildSeriesFriendlyName(Patient pat, Series series)
        {
            try
            {
                switch (series.Modality)
                {
                    case SeriesModality.CT:
                    case SeriesModality.MR:
                    case SeriesModality.PT:
                    case SeriesModality.RTIMAGE:
                        if (series.Images.Any())
                        {
                            foreach (var im in series.Images)
                            {
                                try { if (!double.IsNaN(im.Origin.x)) return im.Id; }
                                catch { }
                            }
                        }
                        return $"{series.Modality} {series.UID.Substring(Math.Max(0, series.UID.Length - 6))}";
                    case SeriesModality.RTSTRUCT:
                        var match = pat.StructureSets.FirstOrDefault(st => st.Image?.HistoryDateTime == series.HistoryDateTime);
                        if (match != null) return match.Id;
                        return "RTSTRUCT";
                    default:
                        return series.Modality.ToString();
                }
            }
            catch { return series.UID; }
        }
    }
}
