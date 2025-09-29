namespace DICOMAnon.Exporter.Helpers
{
    public interface ITreeBuilder
    {
        DicomTree BuildTree(string patientId);
    }
}

