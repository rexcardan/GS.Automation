namespace DICOMAnon.Exporter.Helpers
{
    public class EngineTreeBuilder : ITreeBuilder
    {
        private readonly IEsapiEngineService _engine;
        public EngineTreeBuilder(IEsapiEngineService engine)
        {
            _engine = engine;
        }

        public DicomTree BuildTree(string patientId)
        {
            _engine.Start();
            _engine.EnsureContextAsync().GetAwaiter().GetResult();
            return _engine.WithApp(app => new TreeBuilder().BuildTree(app, patientId));
        }
    }
}

