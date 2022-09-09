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

        static private List<string> _log_ = new List<string>();

        public static OptionSet Options { get; set; } = new OptionSet()
        {
            { "h|?|help", "Help", v => { show_help = v != null; } },
        };

        public static void Main(string[] args)
        {
            MyMain(args);
        }

        private static async void MyMain(string[] args)
        {
            if (args.Length <= 0)
            {
                if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
                    Console.WriteLine(ShowHelp());
            }
            else if (args.Length >= 1)
            {
                var applet = ImageApplets.Applet.GetApplet(args[0]);
                if (applet is ImageApplets.Applet)
                {
                    var extras = applet.ParseOptions(args.Skip(1));
                    if (extras.Count == 0 && !Console.IsInputRedirected && !Console.IsOutputRedirected)
                    {
                        #region Output applet help information
                        Console.Out.WriteLine(ShowHelp(applet.Name));
                        #endregion
                    }
                    else
                    {
                        #region Enum files will be processed from command line
                        var files = new List<string>();
                        foreach (var extra in extras)
                        {
                            var folder = Path.GetDirectoryName(extra);
                            var pattern = Path.GetFileName(extra);
                            folder = Path.IsPathRooted(folder) ? folder : Path.Combine(".", folder);
                            if (Directory.Exists(folder))
                            {
                                files.AddRange(Directory.GetFiles(folder, pattern));
                            }
                        }
                        #endregion

                        #region Output result header
                        var max_len = 72;
                        if (!Console.IsOutputRedirected || applet.Verbose)
                        {
                            Console.Out.WriteLine("Results");
                            Console.Out.WriteLine("".PadRight(Math.Min(LINE_COUNT, max_len + 8), '-'));
                        }
                        _log_.Add("Results");
                        _log_.Add("".PadRight(Math.Min(LINE_COUNT, max_len + 8), '-'));
                        #endregion

                        #region Fetch files from stdin when input redirected
                        if (Console.IsInputRedirected)
                        {
                            if (applet.ReadInputMode == ImageApplets.ReadMode.ALL)
                            {
                                var lines = (await Console.In.ReadToEndAsync()).Split(ImageApplets.Applet.LINE_BREAK, StringSplitOptions.RemoveEmptyEntries);
                                files.AddRange(lines);
                                //max_len = files.Count > 0 ? files.Max(f => f.Length) : 64;
                            }
                            else if (applet.ReadInputMode == ImageApplets.ReadMode.LINE)
                            {
                                while (Console.IsInputRedirected)
                                {
                                    var line = await Console.In.ReadLineAsync();
                                    //var line = Console.In.ReadLine();
                                    if (string.IsNullOrEmpty(line)) break;
                                    RunApplet(line, applet, max_len, extras.ToArray());
                                    System.Threading.Thread.Sleep(25);
                                }
                            }
                            Console.In.Close();
                        }
                        #endregion

                        #region Runing applet
                        if (files.Count > 0)
                        {
                            RunApplet(files, applet, max_len, extras.ToArray());
                        }
                        #endregion

                        #region Out result footer
                        if (Console.IsOutputRedirected)
                            Console.Out.Close();

                        if (!Console.IsOutputRedirected || applet.Verbose )
                            Console.Out.WriteLine("".PadRight(Math.Min(LINE_COUNT, max_len + 8), '-'));

                        _log_.Add("".PadRight(Math.Min(LINE_COUNT, max_len + 8), '-'));
                        #endregion

                        #region Save run log to file When applet set output option with filename
                        if (!string.IsNullOrEmpty(applet.OutputFile))
                        {
                            SaveLogToFile(applet.OutputFile);
                        }
                        #endregion
                    }
                }
                else
                {
                    #region Output full help information
                    if (!Console.IsOutputRedirected) Console.Out.WriteLine(ShowHelp());
                    #endregion
                }
            }
        }

        private static void RunApplet(string file, ImageApplets.Applet applet, int padding, params string[] extras)
        {
            try
            {
                dynamic result = null;
                if (applet is ImageApplets.Applet)
                {
                    applet.ValuePaddingLeft = Math.Max(padding + 3, 4);
                    var ret = applet.Execute(file, out result, extras.Skip(1));
                    if (ret)
                    {
                        var folder = Path.GetDirectoryName(file);
                        var fname = folder.Equals(".") || folder.StartsWith(".\\") ? file.Substring(2) : file;
                        var is_contents = result is string && (result as string).StartsWith("\u20D0");
                        if (is_contents) result = (result as string).Substring(1).Trim();
                        if (!Console.IsOutputRedirected || applet.Verbose)
                        {
                            if (is_contents)
                                Console.Out.WriteLine($"{fname.PadRight(Math.Max(padding, 1))} : {$"{result}"}");
                            else
                                Console.Out.WriteLine($"{fname.PadRight(Math.Max(padding, 1))} : {($"{result}").PadLeft(5)}");
                        }
                        else
                            Console.Out.WriteLine($"{fname}");

                        if (is_contents)
                            _log_.Add($"{fname.PadRight(Math.Max(padding, 1))} : {$"{result}"}");
                        else
                            _log_.Add($"{fname.PadRight(Math.Max(padding, 1))} : {($"{result}").PadLeft(5)}");
                    }
                }
            }
            catch { }
        }

        private static void RunApplet(IEnumerable<string> files, ImageApplets.Applet applet, int padding, params string[] extras)
        {
            if (files is IEnumerable<string>)
            {
                foreach (var file in files)
                {
                    RunApplet(file, applet, padding, extras);
                }
            }
        }

        private static void SaveLogToFile(string file)
        {
            if (!string.IsNullOrEmpty(file))
            {
                var folder = Path.GetDirectoryName(file);
                if (Directory.Exists(string.IsNullOrEmpty(folder) ? "." : folder))
                {
                    using (var fs = new FileStream(file, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    {
                        if (fs.CanSeek) fs.Seek(0, SeekOrigin.Begin);
                        if (fs.CanWrite)
                        {
                            var _kvs_ = _log_.Take(_log_.Count - 1).Skip(2).Select(l =>
                            {
                                var vs = l.Split(':').Select(v => v.Trim());
                                var key = vs.First();
                                var value = vs.Count() > 1 ? l.Replace(key, "").Trim(new char[] { ' ', ':' }) : string.Empty;
                                return (new KeyValuePair<string, string>(key, value));
                            });
                            var _max_key_lens_ = _kvs_.Max(kv => kv.Key.Length);
                            var _max_value_lens_ = _kvs_.Max(kv => kv.Value.Length);
                            var _output_lines_ = new List<string> ();
                            _output_lines_.Add("Result");
                            _output_lines_.Add("".PadRight(_max_value_lens_ <= 5 ? _max_key_lens_ + 8 : 80, '='));
                            _output_lines_.AddRange(_kvs_.Select(kv => $"{kv.Key.PadRight(_max_key_lens_)} : {kv.Value.Trim().PadLeft(_max_value_lens_ <= 5 ? 5 : 0)}"));
                            _output_lines_.Add("".PadRight(_max_value_lens_ <= 5 ? _max_key_lens_ + 8 : 80, '='));
                            var bytes = Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, _output_lines_).Trim());
                            fs.Write(bytes, 0, bytes.Length);
                        }
                    }
                }
            }
        }

        public static string ShowHelp(string applet_name = null, string indent = null)
        {
            var result = string.Empty;
            if (Options is OptionSet)
            {
                using (var sw = new StringWriter())
                {
                    Console.WriteLine($"Usage: {AppName} <Applet> [OPTIONS]+ <ImageFile(s)>");
                    Console.WriteLine("Options:");
                    Options.WriteOptionDescriptions(sw);
                    var applet_names = ImageApplets.Applet.GetApplets();
                    if (applet_names.Count() > 0)
                    {
                        sw.WriteLine($"".PadRight(LINE_COUNT, '='));
                        if (string.IsNullOrEmpty(applet_name) || applet_names.Count(a => a.Equals(applet_name, StringComparison.CurrentCultureIgnoreCase)) <= 0)
                        {
                            sw.WriteLine($"Applets:");
                            sw.WriteLine($"".PadRight(LINE_COUNT, '-'));
                            var applets = applet_names.Select(a => ImageApplets.Applet.GetApplet(a)).OrderBy(a => a.Category);
                            foreach (var applet in applets)
                            {
                                if (applet is ImageApplets.Applet)
                                    sw.WriteLine(applet.Help());
                                if (!applet.Name.Equals(applets.Last().Name))
                                    sw.WriteLine($"".PadRight(LINE_COUNT, '-'));
                            }
                        }
                        else
                        {
                            applet_name = applet_names.Where(a => a.Equals(applet_name, StringComparison.CurrentCultureIgnoreCase)).First();
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
