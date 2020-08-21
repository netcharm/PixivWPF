//css_args /co:/win32icon:./PixivWPF/pixiv-logo.ico
//css_co /win32icon:./PixivWPF/pixiv-logo.ico

//css_reference PresentationFramework.dll

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Windows;
using System.Windows;

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("PixivWPF Search Bridge")]
//[assembly: AssemblyDescription( "PixivWPF Search Bridge" )]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("NetCharm")]
[assembly: AssemblyProduct("PixivWPF Search")]
[assembly: AssemblyCopyright("Copyright NetCharm © 2020")]
[assembly: AssemblyTrademark("NetCharm")]
[assembly: AssemblyCulture("")]

namespace netcharm
{
    class PixivWPFSearch
    {
        private static NamedPipeClientStream pipeClient;
#if DEBUG
        private static string pipeName = "PixivWPF-Search-Debug";
#else
        private static string pipeName = "PixivWPF-Search";
#endif

        private static string[] ParseCommandLine(string cmdline)
        {
            List<string> args = new List<string>();

            string[] cmds = cmdline.Split( new char[] { ' ' } );
            string arg = "";
            foreach (string cmd in cmds)
            {
                if (cmd.StartsWith("\"") && cmd.EndsWith("\""))
                {
                    args.Add(cmd.Trim(new char[] { '\"', ' ' }));
                    arg = "";
                }
                else if (cmd.StartsWith("\""))
                {
                    arg = cmd + " ";
                }
                else if (cmd.EndsWith("\""))
                {
                    arg += cmd;
                    args.Add(arg.Trim(new char[] { '\"', ' ' }));
                    arg = "";
                }
                else if (!string.IsNullOrEmpty(arg))
                {
                    arg += cmd + " ";
                }
                else
                {
                    if (!string.IsNullOrEmpty(cmd))
                    {
                        args.Add(cmd);
                    }
                    arg = "";
                }
#if DEBUG
                Console.WriteLine( $"Curent ARG: {cmd}, Parsed ARG: {arg}" );
#endif
            }
            return (args.GetRange(1, args.Count - 1).ToArray());
        }

        public static void Main(string[] args)
        {
            try
            {
                //var sendData = "83747757";
                //Console.WriteLine(string.Join(Environment.NewLine, args));
                //string[] param = ParseCommandLine(Environment.CommandLine);
                //var sendData = string.Join(Environment.NewLine, param);

                var sendData = string.Join(Environment.NewLine, args);
#if DEBUG
                Console.WriteLine(sendData);
#endif
                using (pipeClient = new NamedPipeClientStream(".", pipeName, 
                    PipeDirection.Out, PipeOptions.Asynchronous, 
                    System.Security.Principal.TokenImpersonationLevel.Impersonation))
                {
                    pipeClient.Connect(1000);
                    using (StreamWriter sw = new StreamWriter(pipeClient))
                    {
                        sw.WriteLine(sendData);
                        sw.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "ERROR!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}