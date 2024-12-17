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
                //if (_pid_ < 0) _pid_ = Environment.ProcessId;
                if (_pid_ < 0) _pid_ = System.Diagnostics.Process.GetCurrentProcess().Id;
                return (_pid_);
            }
        }

        #region Named Pipe Heler
        public class NamedPipeContent
        {
            public string Command { get; set; }
            public bool Scope { get; set; } = false;
            public string[] Args { get; set; }
        }

        private static string _pipe_name_ = string.Empty;

        private NamedPipeServerStream _pipeServer_;
        //private IAsyncResult? _pipeResult_;
        private bool _pipOnClosing_ = false;

        public static string PipeServerName()
        {
#if DEBUG
            return ($"{APP_NAME}-DEBUG");
#else
            return ($"{APP_NAME}");
#endif
        }

        public static string PipeName
        {
            get
            {
                if (string.IsNullOrEmpty(_pipe_name_)) _pipe_name_ = PipeServerName();
                return (_pipe_name_);
            }
        }

        private bool CreateNamedPipeServer()
        {
            try
            {
                ReleaseNamedPipeServer();
                var pipeSec = new PipeSecurity();
                SecurityIdentifier securityIdentifier = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
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
                _pipOnClosing_ = true;
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
                _pipOnClosing_ = false;
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
                if (!_pipOnClosing_ && result != null && result.IsCompleted)
                {
                    using (NamedPipeServerStream ps = result.AsyncState as NamedPipeServerStream)
                    {
                        if (!ps.IsConnected) ps.EndWaitForConnection(result);

                        if (ps.CanRead)
                        {
                            using (StreamReader sw = new StreamReader(ps))
                            {
                                var contents = sw.ReadToEnd().Trim();
                                if (string.IsNullOrEmpty(contents))
                                    Current?.Dispatcher?.Invoke(() => { Current?.MainWindow?.Activate(); });
                                else
                                {
                                    var content = new NamedPipeContent() { Command = "compare", Args = contents.Split(new string[]{ Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries) };
                                    if (content != null)
                                    {
                                        if (content.Command.Equals("active", StringComparison.CurrentCultureIgnoreCase))
                                            Current?.Dispatcher?.Invoke(() => { Current?.MainWindow?.Activate(); });
                                        else if (content.Command.Equals("compare", StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            Current?.Dispatcher?.Invoke(async () =>
                                            {
                                                if (Current?.MainWindow is MainWindow && content.Args.Length > 0)
                                                {
                                                    await (Current?.MainWindow as MainWindow).LoadImageFromFiles(content.Args);
                                                }
                                            });
                                        }
                                    }
                                }
                            }
                        }

                        if (ps.IsConnected) ps.Disconnect();
                    }
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

        private static string[] GetPipeServer(string server = ".")
        {
            return (Directory.GetFiles($"\\\\{server}\\pipe\\", $"{PipeName}-*").Select(p => p.Replace($"\\\\{server}\\pipe\\", "")).ToArray());
        }

        public static bool DetectPipeServer(string server = ".")
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

        public static bool SendToPipeServer(string content, string server = ".")
        {
            var result = false;
            try
            {
                var pipes = GetPipeServer();
                if (pipes.Length > 0 && !string.IsNullOrEmpty(content))
                {
                    var pipe = pipes.First();
                    using (var pipeClient = new NamedPipeClientStream(server, pipe, PipeDirection.Out, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Impersonation))
                    {
                        pipeClient.Connect(server.Equals(".") ? 1000 : 5000);
                        using (StreamWriter sw = new StreamWriter(pipeClient))
                        {
                            sw.WriteLine(content);
                            sw.Flush();
                        }
                    }
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
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            ReleaseNamedPipeServer();
        }
    }
}
