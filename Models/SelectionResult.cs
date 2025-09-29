using System;
using System.Collections.Generic;

namespace DICOMAnon.Exporter.Models
{
    public class SelectionInstance : IEquatable<SelectionInstance>
    {
        public string SeriesUid { get; set; }
        public string InstanceUid { get; set; }

        public bool Equals(SelectionInstance other)
        {
            if (other is null) return false;
            return string.Equals(SeriesUid, other.SeriesUid, StringComparison.Ordinal)
                && string.Equals(InstanceUid, other.InstanceUid, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as SelectionInstance);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (SeriesUid == null ? 0 : SeriesUid.GetHashCode());
                hash = hash * 23 + (InstanceUid == null ? 0 : InstanceUid.GetHashCode());
                return hash;
            }
        }
    }

    public class SelectionResult
    {
        public HashSet<string> SeriesUids { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<SelectionInstance> Instances { get; } = new HashSet<SelectionInstance>();
        public string PatientId { get; set; }
    }
}
