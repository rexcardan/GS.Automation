using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using V = VMS.TPS.Common.Model.API;

namespace DICOMAnon.Exporter.Helpers
{
    namespace EsapiX.Core
    {
        public class EsapiEngine : IDisposable
        {
            private BlockingCollection<Task> _vmsJobs = new BlockingCollection<Task>();
            private BlockingCollection<Task> _nonVMSJobs = new BlockingCollection<Task>();
            private static EsapiEngine instance = null;
            private static readonly object padlock = new object();
            private Thread vmsThread;
            private Thread nonVMSThread;
            private CancellationTokenSource cts;
            V.Application _app;
            private static Microsoft.Extensions.Logging.ILogger _logger;

            public EsapiEngine(ILogger logger)
            {
                cts = new CancellationTokenSource();
                nonVMSThread = new Thread(NonVMSThreadStart)
                {
                    Name = "ACT NonVMS",
                    IsBackground = true
                };
                nonVMSThread.SetApartmentState(ApartmentState.STA);
                nonVMSThread.Start();

                vmsThread = new Thread(VMSThreadStart)
                {
                    Name = "ACT VMS",
                    IsBackground = true
                };
                vmsThread.SetApartmentState(ApartmentState.STA);
                vmsThread.Start();
            }

            private void NonVMSThreadStart()
            {
                foreach (var job in _nonVMSJobs.GetConsumingEnumerable(cts.Token))
                {
                    try
                    {
                        job.RunSynchronously();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.Message);
                        OnExceptionRaisedHandler(ex);
                    }
                }
            }

            private void VMSThreadStart()
            {
                foreach (var job in _vmsJobs.GetConsumingEnumerable(cts.Token))
                {
                    try
                    {
                        job.RunSynchronously();
                    }
                    catch (Exception ex)
                    {
                        NonVMSInvoke(() =>
                        {
                            _logger.LogError(ex.Message);
                            OnExceptionRaisedHandler(ex);
                        });
                    }
                }
            }

            public Task SetContext(Func<VMS.TPS.Common.Model.API.Application> createAppFunc)
            {
                return InvokeAsync(new Action(() =>
                {
                    _app = null;
                    try
                    {
                        _app = createAppFunc();
                    }
                    catch (Exception e)
                    {
                        _logger?.LogError(e, "Failed to create VMS Application context.");
                        throw e;
                    }
                    NonVMSInvoke(() =>
                    {
                        RaiseContextLoaded();
                    });
                }));
            }

            public T GetValue<T>(Func<V.Application, T> sacFunc)
            {
                T toReturn = default(T);
                Invoke(() =>
                {
                    toReturn = sacFunc(_app);
                });
                return toReturn;
            }

            public async Task<T> GetValueAsync<T>(Func<V.Application, T> sacFunc)
            {
                T toReturn = default(T);
                await InvokeAsync(() =>
                {
                    toReturn = sacFunc(_app);
                });
                return toReturn;
            }

            public void Execute(Action<V.Application> sacOp)
            {
                Invoke(() =>
                {
                    sacOp(_app);
                });
            }

            public Task ExecuteAsync(Action<V.Application> sacOp)
            {
                return InvokeAsync(() =>
                {
                    sacOp(_app);
                });
            }

            public Task InvokeAsync(Action action)
            {
                var task = new Task(action, cts.Token);
                _vmsJobs.Add(task);
                return task;
            }

            public void Invoke(Action action)
            {
                var task = new Task(action, cts.Token);
                _vmsJobs.Add(task);
                try
                {
                    task.GetAwaiter().GetResult();
                    if (task.IsFaulted)
                    {
                        throw task.Exception;
                    }
                }
                catch (Exception ex)
                {
                    NonVMSInvoke(() =>
                    {
                        _logger.LogError(ex.Message);
                        OnExceptionRaisedHandler(ex);
                    });
                    throw;
                }
            }

            private Task NonVMSInvoke(Action action)
            {
                var task = new Task(action);
                _nonVMSJobs.Add(task);
                return task;
            }

            public void DisposeVMS()
            {
                Invoke(new Action(() =>
                {
                    if (_app != null)
                    {
                        _app?.Dispose();
                        _app = null;
                    }
                }));
            }

            public void Dispose()
            {
                DisposeVMS();
                if (!_vmsJobs.IsAddingCompleted)
                {
                    while (_vmsJobs.Count > 0)
                    {
                        Task item;
                        _vmsJobs.TryTake(out item);
                    }
                    _vmsJobs.CompleteAdding();
                    _nonVMSJobs.CompleteAdding();
                }
            }

            public int ThreadId => vmsThread.ManagedThreadId;

            public delegate void ExceptionRaisedHandler(Exception ex);
            public event ExceptionRaisedHandler ExceptionRaised;

            public void OnExceptionRaisedHandler(Exception ex)
            {
                ExceptionRaised?.Invoke(ex);
            }

            public event EventHandler ContextLoaded;

            public void RaiseContextLoaded()
            {
                ContextLoaded?.Invoke(this, EventArgs.Empty);
            }

            public event EventHandler ThreadStopping;

            public void RaiseThreadStopping()
            {
                ThreadStopping?.Invoke(this, EventArgs.Empty);
            }
        }
    }

}
