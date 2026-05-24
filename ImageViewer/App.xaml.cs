using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ImageViewer
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private static readonly string APP_NAME = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);

        private static void ReportMessage(Exception ex)
        {
            Current?.Dispatcher?.Invoke(() =>
            {
                if (Current?.MainWindow?.IsLoaded ?? false)
                {
                    Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message + Environment.NewLine + ex.StackTrace);
                }
            });
        }

        private static int _pid_ = -1;
        public static int PID
        {
            get
            {
                //if (_pid_ <= 0) _pid_ = Environment.ProcessId;
                if (_pid_ <= 0) _pid_ = System.Diagnostics.Process.GetCurrentProcess().Id;
                return (_pid_);
            }
        }

        #region Named Pipe Helper
        public class NamedPipeContent
        {
            public string Command { get; set; }
            public bool Scope { get; set; } = false;
            public string[] Args { get; set; }
        }

        private static string _pipe_name_ = string.Empty;
        public static string PipeName
        {
            get
            {
                if (string.IsNullOrEmpty(_pipe_name_))
                {
#if DEBUG
                    _pipe_name_ = $"{APP_NAME}-DEBUG";
#else
                    _pipe_name_ = APP_NAME;
#endif
                }
                return (_pipe_name_);
            }
        }

        private NamedPipeServerStream _pipeServer_;
        //private IAsyncResult? _pipeResult_;
        private bool _pipeOnClosing_ = false;

        private bool CreateNamedPipeServer()
        {
            try
            {
                ReleaseNamedPipeServer();
                var pipeSec = new PipeSecurity();
                SecurityIdentifier securityIdentifier = new(WellKnownSidType.AuthenticatedUserSid, null);
                pipeSec.AddAccessRule(new PipeAccessRule(securityIdentifier, PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
                //pipeSec.SetAccessRule(new PipeAccessRule("Everyone", PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow));
                _pipeServer_ = new NamedPipeServerStream($"{PipeName}-{PID}", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, pipeSecurity: pipeSec);
                _pipeServer_.BeginWaitForConnection(PipeReceiveData, _pipeServer_);
            }
            catch (Exception ex) { ReportMessage(ex); }

            return (true);
        }

        private bool ReleaseNamedPipeServer()
        {
            if (_pipeServer_ != null)
            {
                _pipeOnClosing_ = true;
                try
                {
                    if (_pipeServer_.IsConnected) _pipeServer_?.Disconnect();
                }
                catch (Exception ex) { ReportMessage(ex); }
                try
                {
                    _pipeServer_?.Close();
                }
                catch (Exception ex) { ReportMessage(ex); }
                try
                {
                    _pipeServer_?.Dispose();
                }
                catch (Exception ex) { ReportMessage(ex); }
                _pipeServer_ = null;
                _pipeOnClosing_ = false;
            }
            return (true);
        }

        private bool WaitOnNamedPipeServer()
        {
            return (CreateNamedPipeServer());
        }

        private void PipeReceiveData(IAsyncResult result)
        {
            try
            {
                if (!_pipeOnClosing_ && result != null && result.IsCompleted)
                {
                    using NamedPipeServerStream ps = result.AsyncState as NamedPipeServerStream;
                    if (!ps.IsConnected) ps.EndWaitForConnection(result);

                    if (ps.CanRead)
                    {
                        using StreamReader sw = new(ps);
                        var contents = sw.ReadToEnd().Trim();
                        if (string.IsNullOrEmpty(contents))
                            Current?.Dispatcher?.Invoke(() => { Current?.MainWindow?.Activate(); });
                        else
                        {
                            var content = new NamedPipeContent() { Command = "view", Args = contents.Split([Environment.NewLine, "\r\n", "\n\r", "\r", "\n" ], StringSplitOptions.RemoveEmptyEntries) };
                            if (content?.Args.Length > 0 && Current?.MainWindow is MainWindow)
                            {
                                Current?.Dispatcher?.Invoke(async () =>
                                {
                                    await (Current?.MainWindow as MainWindow).LoadImageFromFiles(content.Args);
                                });
                            }
                        }
                    }
                    if (ps.IsConnected) ps.Disconnect();
                }
            }
            catch (Exception ex) { ReportMessage(ex); }
            finally
            {
                Current?.Dispatcher?.Invoke(() =>
                {
                    if (Current?.MainWindow?.WindowState == System.Windows.WindowState.Minimized)
                    {
                        Current.MainWindow.WindowState = System.Windows.WindowState.Normal;
                    }
                    Current?.MainWindow?.Activate();
                });
                WaitOnNamedPipeServer();
            }
        }

        private string[] GetPipeServer(string server = ".")
        {
            return ([.. Directory.GetFiles($"\\\\{server}\\pipe\\", $"{PipeName}-*").Select(p => p.Replace($"\\\\{server}\\pipe\\", ""))]);
        }

        public bool DetectPipeServer(string server = ".")
        {
            var result = false;
            try
            {
                var pipes = GetPipeServer(server);
                result = pipes.Length > 0;
            }
            catch (Exception ex) { ReportMessage(ex); }
            return (result);
        }

        public bool SendToPipeServer(string content, string server = ".")
        {
            var result = false;
            try
            {
                var pipes = GetPipeServer();
                if (pipes.Length > 0 && !string.IsNullOrEmpty(content))
                {
                    var pipe = pipes.First();
                    using var pipeClient = new NamedPipeClientStream(server, pipe, PipeDirection.Out, PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
                    pipeClient.Connect(server.Equals(".") ? 1000 : 5000);
                    using StreamWriter sw = new(pipeClient);
                    sw.WriteLine(content);
                    sw.Flush();
                }
            }
            catch (Exception ex) { ReportMessage(ex); }
            return (result);
        }
        #endregion

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var opts = this.GetCmdLineOpts();
            var args = opts.Args.ToArray();

            #region Unhandled Exception Handler
            TaskScheduler.UnobservedTaskException += (s, ev) =>
            {
                if (ev.Exception != null)
                {
                    ReportMessage(ev.Exception);
                    ev.SetObserved();
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                if (ev.ExceptionObject is Exception ex && (ev.ExceptionObject is not TaskCanceledException))
                {
                    ReportMessage(ex);
                }
            };

            Dispatcher.UnhandledException += (s, ev) =>
            {
                if (ev.Exception != null && (ev.Exception is not TaskCanceledException))
                {
                    ReportMessage(ev.Exception);
                }
                ev.Handled = MainWindow?.IsLoaded ?? false;
            };

            DispatcherUnhandledException += (s, ev) =>
            {
                if (ev.Exception != null && (ev.Exception is not TaskCanceledException))
                {
                    ReportMessage(ev.Exception);
                }
                ev.Handled = MainWindow?.IsLoaded ?? false;
            };
            #endregion

            #region Named Pipe Server
            if (opts.Singleton && DetectPipeServer())
            {
                if (args.Length > 0)
                {
                    var content = new NamedPipeContent(){ Command = "query", Args = args };
                    //SendToPipeServer(Newtonsoft.Json.JsonConvert.SerializeObject(content, Newtonsoft.Json.Formatting.Indented).ToString());
                    SendToPipeServer(string.Join(Environment.NewLine, content.Args));
                }
                else
                {
                    //var content = new NamedPipeContent(){ Command = "active" };
                    //SendToPipeServer(Newtonsoft.Json.JsonConvert.SerializeObject(content, Newtonsoft.Json.Formatting.Indented).ToString());
                    SendToPipeServer("");
                }
                Shutdown();
                Environment.Exit(0);
            }
            CreateNamedPipeServer();
            #endregion
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            #region
            ReleaseNamedPipeServer();
            #endregion
        }
    }
}
