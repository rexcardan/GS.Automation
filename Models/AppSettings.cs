using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Serialization;

namespace DICOMAnon.Exporter.Models
{
    [DataContract]
    public class AppSettings
    {
        [DataMember(Order = 1)]
        public string DICOMAnonIPAddress { get; set; } = "127.0.0.1";

        [DataMember(Order = 2)]
        public string DICOMAnonAPIKey { get; set; } = string.Empty;

        [DataMember(Order = 3)]
        public int DICOMAnonPort { get; set; } = 13997;

        [DataMember(Order = 99)]
        public int Version { get; set; } = 1;
    }
}
