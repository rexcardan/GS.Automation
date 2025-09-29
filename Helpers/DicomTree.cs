using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DICOMAnon.Exporter.Helpers
{
    public enum DicomNodeType
    {
        Course,
        Plan,
        Dose,
        StructureSet,
        Image,
        Series,
        Instance
    }

    public class DicomNode
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public DicomNodeType Type { get; set; }
        public string SeriesUID { get; set; }
        public string InstanceUID { get; set; }
        public List<DicomNode> Children { get; set; } = new List<DicomNode>();
    }

    public class DicomTree
    {
        public List<DicomNode> Courses { get; set; } = new List<DicomNode>();
        public List<DicomNode> Series { get; set; } = new List<DicomNode>();
    }
}
