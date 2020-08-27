//// /ew option for compiling to windows execution file
//css_args /co:/win32icon:./PixivWPF/pixiv-logo.ico
//css_co /win32icon:./PixivWPF/pixiv-logo.ico

//css_reference PresentationFramework.dll

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
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
        public static void Main(string[] args)
        {
            try
            {
                if (args.Length < 1) return;
                //args = Environment.GetCommandLineArgs();
                var sendData = string.Join(Environment.NewLine, args);
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
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "ERROR!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}