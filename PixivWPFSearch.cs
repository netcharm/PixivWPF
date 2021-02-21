//// /ew option for compiling to windows execution file
//// /e  option for compiling to console execution file
//css_args /co:/win32icon:./PixivWPF/pixiv-logo.ico
//css_co /win32icon:./PixivWPF/pixiv-logo.ico

//css_reference PresentationFramework.dll

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Windows;

using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("PixivWPF Search Bridge Utility")]
//[assembly: AssemblyDescription("PixivWPF Search Bridge Utility")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("NetCharm")]
[assembly: AssemblyProduct("PixivWPF Search Bridge Utility")]
[assembly: AssemblyCopyright("Copyright NetCharm © 2020")]
[assembly: AssemblyTrademark("NetCharm")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

namespace netcharm
{
    class PixivWPFSearch
    {
        private static string AppPath = Path.GetDirectoryName(Application.ResourceAssembly.CodeBase.ToString()).Replace("file:\\", "");
        public static void Main(string[] args)
        {
            try
            {
                if (args.Length < 1) return;
                //args = Environment.GetCommandLineArgs();
                if (args[0].Equals("upgrade", StringComparison.CurrentCultureIgnoreCase))
                    UpgradeFiles(args.Skip(1).ToArray());
                else
                    SendBridgeCmd(args);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "ERROR!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void SendBridgeCmd(string[] datas)
        {
            var sendData = string.Join(Environment.NewLine, datas);
            if (string.IsNullOrEmpty(sendData.Trim())) return;

            var pipes = System.IO.Directory.GetFiles("\\\\.\\pipe\\", "PixivWPF*");
#if DEBUG
                if (pipes.Length > 0)
                {
                    Console.WriteLine($"Found {pipes.Length} PixivWPF-Search Bridge(s):");
                    foreach (var pipe in pipes)
                    {
                        Console.WriteLine($"  {pipe}");
                    }
                }
                else return;
#endif

            foreach (var pipe in pipes)
            {
                try
                {
                    var pipeName = pipe.Substring(9);
                    using (var pipeClient = new NamedPipeClientStream(".", pipeName,
                        PipeDirection.Out, PipeOptions.Asynchronous,
                        System.Security.Principal.TokenImpersonationLevel.Impersonation))
                    {
                        pipeClient.Connect(1000);
                        using (StreamWriter sw = new StreamWriter(pipeClient))
                        {
#if DEBUG
                                Console.WriteLine($"Sending [{sendData}] to {pipeName}");
#endif
                            sw.WriteLine(sendData);
                            sw.Flush();
                        }
                    }
                }
#if DEBUG
                    catch(Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "ERROR!", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
#else
                catch (Exception) { }
#endif
            }
        }

        private static void UpgradeFiles(string[] files)
        {
            if (files.Length <= 0) return;
            var wait_count = 0;
            do
            {
                if (System.IO.Directory.GetFiles("\\\\.\\pipe\\", "PixivWPF*").Count() <= 0) break;
                System.Threading.Thread.Sleep(1000);
                wait_count++;
                if (wait_count > 30) return;
            }while (wait_count < 30);
            System.Threading.Tasks.Task.Delay(250).GetAwaiter().GetResult();

            List<string> f_upgraded = new List<string>();
            List<string> f_skiped = new List<string>();
            foreach (var f_remote in files)
            {
                var fn = Path.GetFileName(f_remote);
                var f_local = Path.Combine(AppPath, fn);
                if (!File.Exists(f_remote)) continue;
                var fi_remote = new FileInfo(f_remote);
                if (!File.Exists(f_local) || new FileInfo(f_local).LastWriteTime < fi_remote.LastWriteTime)
                {
                    f_upgraded.Add($"Upgrade file ...");
                    f_upgraded.Add($"  From : {f_remote}");
                    f_upgraded.Add($"  To   : {f_local}");
                    File.Copy(f_remote, f_local, true);
                }
                else
                {
                    f_skiped.Add($"Skiped file ...");
                    f_skiped.Add($"  From : {f_remote}");
                    f_skiped.Add($"  To   : {f_local}");
                }
            }
            if (f_upgraded.Count > 0)
            {
                f_upgraded.Insert(0, "Upgraded Finished!");
                f_upgraded.Add("Start Application?");
                var ret = MessageBox.Show(string.Join(Environment.NewLine, f_upgraded), "Congratulation!", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (ret == MessageBoxResult.OK || ret == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(Path.Combine(AppPath, "PixivWPF.exe"));
                }
            }
            else
            {
                var ret = MessageBox.Show("Start Application?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (ret == MessageBoxResult.OK || ret == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(Path.Combine(AppPath, "PixivWPF.exe"));
                }
            }
        }
    }
}