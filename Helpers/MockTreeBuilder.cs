using System.Collections.Generic;

namespace DICOMAnon.Exporter.Helpers
{
    public class MockTreeBuilder : ITreeBuilder
    {
        public DicomTree BuildTree(string patientId)
        {
            // Build a mock tree with representative structure
            var tree = new DicomTree();

            // Courses -> Plans -> Dose/StructureSet/Image
            var course1 = new DicomNode { Id = "C1", DisplayName = "Course C1", Type = DicomNodeType.Course };
            var plan1 = new DicomNode { Id = "P1", DisplayName = "Plan 1", Type = DicomNodeType.Plan };
            plan1.Children.Add(new DicomNode { Id = "DOSE-P1", DisplayName = "Dose (Plan 1)", Type = DicomNodeType.Dose, SeriesUID = "SER-C1-P1-DOSE" });
            plan1.Children.Add(new DicomNode { Id = "SS-P1", DisplayName = "Structure Set (Plan 1)", Type = DicomNodeType.StructureSet, SeriesUID = "SER-C1-P1-SS" });
            plan1.Children.Add(new DicomNode { Id = "IMG-P1", DisplayName = "Image (Plan 1)", Type = DicomNodeType.Image, SeriesUID = "SER-C1-P1-IMG" });

            var plan2 = new DicomNode { Id = "P2", DisplayName = "Plan 2", Type = DicomNodeType.Plan };
            plan2.Children.Add(new DicomNode { Id = "DOSE-P2", DisplayName = "Dose (Plan 2)", Type = DicomNodeType.Dose, SeriesUID = "SER-C1-P2-DOSE" });
            plan2.Children.Add(new DicomNode { Id = "SS-P2", DisplayName = "Structure Set (Plan 2)", Type = DicomNodeType.StructureSet, SeriesUID = "SER-C1-P2-SS" });
            plan2.Children.Add(new DicomNode { Id = "IMG-P2", DisplayName = "Image (Plan 2)", Type = DicomNodeType.Image, SeriesUID = "SER-C1-P2-IMG" });

            course1.Children.Add(plan1);
            course1.Children.Add(plan2);

            var course2 = new DicomNode { Id = "C2", DisplayName = "Course C2", Type = DicomNodeType.Course };
            var plan3 = new DicomNode { Id = "P3", DisplayName = "Plan 3", Type = DicomNodeType.Plan };
            plan3.Children.Add(new DicomNode { Id = "SS-P3", DisplayName = "Structure Set (Plan 3)", Type = DicomNodeType.StructureSet, SeriesUID = "SER-C2-P3-SS" });
            course2.Children.Add(plan3);

            tree.Courses.Add(course1);
            tree.Courses.Add(course2);

            // Series -> Instances
            var series1 = new DicomNode { Id = "SER-0001", DisplayName = "CT Series 1", Type = DicomNodeType.Series, SeriesUID = "SER-0001" };
            series1.Children.AddRange(MockInstances("SER-0001", 10));

            var series2 = new DicomNode { Id = "SER-0002", DisplayName = "MR Series 2", Type = DicomNodeType.Series, SeriesUID = "SER-0002" };
            series2.Children.AddRange(MockInstances("SER-0002", 3));

            var series3 = new DicomNode { Id = "SER-0003", DisplayName = "RTSTRUCT Series", Type = DicomNodeType.Series, SeriesUID = "SER-0003" };
            // Structural series may not list instances here in mock

            tree.Series.Add(series1);
            tree.Series.Add(series2);
            tree.Series.Add(series3);

            return tree;
        }

        private static List<DicomNode> MockInstances(string seriesUid, int count)
        {
            var list = new List<DicomNode>();
            for (int i = 1; i <= count; i++)
            {
                var instUid = $"{seriesUid}-INST-{i:000}";
                list.Add(new DicomNode
                {
                    Id = instUid,
                    DisplayName = $"Instance {i:000}",
                    Type = DicomNodeType.Instance,
                    SeriesUID = seriesUid,
                    InstanceUID = instUid
                });
            }
            return list;
        }
    }
}

