using VMS.TPS.Common.Model.API;

namespace DICOMAnon.Exporter.Helpers
{
    public class EsapiTreeBuilder : ITreeBuilder
    {
        public DicomTree BuildTree(string patientId)
        {
            using (var app = Application.CreateApplication())
            {
                var impl = new TreeBuilder();
                return impl.BuildTree(app, patientId);
            }
        }
    }
}

