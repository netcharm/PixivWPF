using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Mono.Options;

namespace ImageAppletsCLI
{
    class Program
    {
        static private string AppName = Path.GetFileName(AppDomain.CurrentDomain.FriendlyName);
        static private string AppPath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        static private string WorkPath = Directory.GetCurrentDirectory();

        static private int LINE_COUNT = 80;
        static private bool show_help = false;

        public static OptionSet Options { get; set; } = new OptionSet()
        {
            { "h|?|help", "Help", v => { show_help = v != null; } },
        };

        public static void Main(string[] args)
        {
            //Console.WriteLine(args[0]);
            //Console.WriteLine(string.Join(Environment.NewLine, args));
            if (args.Length <= 0)
            {
                if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
                    Console.WriteLine(Help());
            }
            else if (args.Length >= 1)
            {
                var applet = ImageApplets.Applet.GetApplet(args[0]);
                if (applet is ImageApplets.Applet)
                {
                    if (args.Length == 1 && !Console.IsInputRedirected && !Console.IsOutputRedirected)
                    {
                        Console.WriteLine(Help(applet.Name));
                    }
                    else
                    {
                        var extras = applet.Options.Parse(args.Skip(1));
                        var files = new List<string>();
                        if (Console.IsInputRedirected)
                        {
                            var lines = Console.In.ReadToEnd().Split(ImageApplets.Applet.LINE_BREAK, StringSplitOptions.RemoveEmptyEntries);
                            files.AddRange(lines);
                        }

                        foreach (var extra in extras)
                        {
                            var folder = Path.GetDirectoryName(extra);
                            var pattern = Path.GetFileName(extra);
#if DEBUG
                            if (!Console.IsOutputRedirected)
                            {
                                Console.WriteLine(Path.IsPathRooted(folder) ? folder : Path.GetFullPath(Path.Combine(WorkPath, folder)));
                                Console.WriteLine(pattern);
                            }
#endif
                            //files.AddRange(Directory.GetFiles(Path.IsPathRooted(folder) ? folder : Path.GetFullPath(Path.Combine(WorkPath, folder)), pattern));
                            folder = Path.IsPathRooted(folder) ? folder : Path.Combine(".", folder);
                            if (Directory.Exists(folder))
                            {
                                files.AddRange(Directory.GetFiles(folder, pattern));
                            }
                        }
#if DEBUG
                        if (!Console.IsOutputRedirected)
                        {
                            Console.WriteLine("".PadRight(LINE_COUNT, '~'));
                            Console.WriteLine(string.Join(Environment.NewLine, files));
                            Console.WriteLine("".PadRight(LINE_COUNT, '~'));
                        }
#endif
                        if (files.Count > 0)
                        {
                            var max_len = files.Max(f => f.Length);
                            if (!Console.IsOutputRedirected)
                            {
                                Console.WriteLine("Results");
                                Console.WriteLine("".PadRight(Math.Max(LINE_COUNT, max_len + 10), '-'));
                            }
                            foreach (var file in files)
                            {
                                dynamic result = null;
                                var ret = applet.Execute(file, out result, extras.Skip(1));
                                if (ret)
                                {
                                    if (Console.IsOutputRedirected)
                                        Console.Out.WriteLine($"{(file.StartsWith(".\\") ? file.Substring(2) : file)}");
                                    else
                                        Console.WriteLine($"{(file.StartsWith(".\\") ? file.Substring(2) : file).PadRight(max_len + 1)} \t: {($"{result}").PadLeft(5)}");
                                }
                            }
                            if (Console.IsOutputRedirected)
                                Console.Out.Close();
                            else
                                Console.WriteLine("".PadRight(Math.Max(LINE_COUNT, max_len + 10), '-'));
                        }
                    }
                }
                else
                {
                    if (!Console.IsOutputRedirected)
                    {
                        Console.WriteLine(Help());
                    }
                }
            }
        }

        public static string Help(string applet_name = null, string indent = null)
        {
            var result = string.Empty;
            if (Options is OptionSet)
            {
                using (var sw = new StringWriter())
                {
                    Console.WriteLine($"Usage: {AppName} <Applet> [OPTIONS]+ <ImageFile(s)>");
                    Console.WriteLine("Options:");
                    Options.WriteOptionDescriptions(sw);
                    var applets = ImageApplets.Applet.GetApplets();
                    if (applets.Count() > 0)
                    {
                        sw.WriteLine($"".PadRight(LINE_COUNT, '='));
                        if (string.IsNullOrEmpty(applet_name) || applets.Count(a => a.Equals(applet_name, StringComparison.CurrentCultureIgnoreCase)) <= 0)
                        {
                            sw.WriteLine($"Applets:");
                            sw.WriteLine($"".PadRight(LINE_COUNT, '-'));
                            foreach (var applet in applets)
                            {
                                //sw.WriteLine($"{indent}{applet}");
                                var instance = ImageApplets.Applet.GetApplet(applet);
                                if (instance is ImageApplets.Applet)
                                    sw.WriteLine(instance.Help());
                                if (applet != applets.Last())
                                    sw.WriteLine($"".PadRight(LINE_COUNT, '-'));
                            }
                        }
                        else
                        {
                            applet_name = applets.Where(a => a.Equals(applet_name, StringComparison.CurrentCultureIgnoreCase)).First();
                            var instance = ImageApplets.Applet.GetApplet(applet_name);
                            if (instance is ImageApplets.Applet)
                                sw.WriteLine(instance.Help());
                        }
                        sw.WriteLine($"".PadRight(LINE_COUNT, '='));
                    }
                    result = sw.ToString();
                }
            }
            return (result);
        }
    }
}
