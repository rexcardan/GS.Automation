using System;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;

namespace DICOMAnon.Exporter.Helpers
{
    public interface IEsapiEngineService : IDisposable
    {
        bool IsStarted { get; }
        Task StartAsync();
        void Start();
        void Stop();
        Task EnsureContextAsync();
        T WithApp<T>(Func<Application, T> func);
        Task<T> WithAppAsync<T>(Func<Application, T> func);
        event EventHandler ContextReady;
    }
}

