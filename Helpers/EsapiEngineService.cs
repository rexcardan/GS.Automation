using System;
using System.Threading.Tasks;
using DICOMAnon.Exporter.Helpers.EsapiX.Core;
using Microsoft.Extensions.Logging;
using VMS.TPS.Common.Model.API;

namespace DICOMAnon.Exporter.Helpers
{
    public class EsapiEngineService : IEsapiEngineService
    {
        private readonly ILogger _logger;
        private EsapiEngine _engine;
        private bool _started;

        public EsapiEngineService(ILogger logger = null)
        {
            _logger = logger ?? new SimpleConsoleLogger();
        }

        public bool IsStarted => _started;

        public async Task StartAsync()
        {
            if (_started) return;
            _engine = new EsapiEngine(_logger);
            _engine.ContextLoaded += (_, __) => ContextReady?.Invoke(this, EventArgs.Empty);
            _started = true;
            // Preload ESAPI application context so it's ready
            await EnsureContextAsync();
        }

        public void Start()
        {
            StartAsync().GetAwaiter().GetResult();
        }

        public async Task EnsureContextAsync()
        {
            if (!_started)
                await StartAsync();
            await _engine.SetContext(() => Application.CreateApplication());
        }

        public T WithApp<T>(Func<Application, T> func)
        {
            if (!_started) Start();
            return _engine.GetValue(func);
        }

        public Task<T> WithAppAsync<T>(Func<Application, T> func)
        {
            if (!_started) Start();
            return _engine.GetValueAsync(func);
        }

        public void Stop()
        {
            _engine?.Dispose();
            _engine = null;
            _started = false;
        }

        public event EventHandler ContextReady;

        public void Dispose()
        {
            Stop();
        }
    }

    // Minimal console logger to satisfy EsapiEngine dependency
    public class SimpleConsoleLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            try
            {
                var msg = formatter != null ? formatter(state, exception) : state?.ToString();
                if (!string.IsNullOrEmpty(msg)) Console.WriteLine($"[{logLevel}] {msg}");
                if (exception != null) Console.WriteLine(exception);
            }
            catch { }
        }
    }
}

