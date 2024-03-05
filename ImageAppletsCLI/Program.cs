using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Mono.Options;
using System.Windows;
using System.Threading;

namespace ImageAppletsCLI
{
    class Program
    {
        static private string AppName = Path.GetFileName(AppDomain.CurrentDomain.FriendlyName);
        static private string AppPath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        static private string WorkPath = Directory.GetCurrentDirectory();

        static private int LINE_COUNT = 80;
        static private bool show_help = false;
        static private string[] LINE_BREAK = new string[] { "\n\r", "\r\n", "\r", "\n", Environment.NewLine };

        static private List<string> _log_ = new List<string>();
        static private List<string> _flist_out_ = new List<string>();

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
                    if ((extras.Count == 0 && string.IsNullOrEmpty(applet.InputFile)) && !Console.IsInputRedirected && !Console.IsOutputRedirected)
                    {
                        #region Output applet help information
                        Console.Out.WriteLine(ShowHelp(applet.Name));
                        #endregion
                    }
                    else
                    {
                        var files = new List<string>();
                        #region Enum from input filelist file
                        if (!string.IsNullOrEmpty(applet.InputFile))
                        {
                            try
                            {
                                if (applet.InputFile.Equals(ImageApplets.Applet.ClipboardName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    try
                                    {
                                        Thread clip = new Thread(new ThreadStart(delegate()
                                        {
                                            try
                                            {
                                                var _flist_in_ = new List<string>();
                                                DataObject dp = new DataObject();
                                                if (Clipboard.ContainsFileDropList())
                                                {
                                                    foreach (var f in Clipboard.GetFileDropList()) _flist_in_.Add(f);
                                                }
                                                else if (Clipboard.ContainsText())
                                                {
                                                    _flist_in_.AddRange(Clipboard.GetText().Split(LINE_BREAK, StringSplitOptions.RemoveEmptyEntries));
                                                }
                                                files.AddRange(_flist_in_);
                                            }
                                            catch (Exception ex) { Console.WriteLine(ex.Message); }
                                        }));
                                        clip.TrySetApartmentState(ApartmentState.STA);
                                        clip.Start();
                                        clip.Join();
                                    }
                                    catch (Exception ex) { Console.WriteLine(ex.Message); }
                                }
                                else if (File.Exists(applet.InputFile))
                                {
                                    files.AddRange(File.ReadLines(applet.InputFile, Encoding.UTF8));
                                }
                            }
                            catch (Exception ex) { Console.WriteLine(ex.Message); }
                        }
                        #endregion

                        #region Enum files will be processed from command line
                        var sopt = applet.Recursion ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                        foreach (var extra in extras)
                        {
                            var folder = Path.GetDirectoryName(extra);
                            var pattern = Path.GetFileName(extra);
                            folder = Path.IsPathRooted(folder) ? folder : Path.Combine(".", folder);
                            if (Directory.Exists(folder))
                            {
                                //files.AddRange(Directory.GetFiles(folder, pattern, sopt));
                                files.AddRange(Directory.EnumerateFiles(folder, pattern, sopt));
                            }
                        }
                        #endregion

                        #region Output result header
                        var max_len = 72;
                        if (!Console.IsOutputRedirected || applet.Verbose)
                        {
                            Console.Out.WriteLine("Results");
                            Console.Out.WriteLine("".PadRightCJK(Math.Min(LINE_COUNT, max_len + 8), '-'));
                        }
                        _log_.Add("Results");
                        _log_.Add("".PadRightCJK(Math.Min(LINE_COUNT, max_len + 8), '-'));
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
                            if (applet.Sorting)
                                files = ImageApplets.Applet.NaturalSort(files, padding: applet.SortZero, descending: applet.Descending).ToList();
                            RunApplet(files, applet, max_len, extras.ToArray());
                        }
                        #endregion

                        #region Out result footer
                        if (Console.IsOutputRedirected)
                            Console.Out.Close();

                        if (!Console.IsOutputRedirected || applet.Verbose)
                        {
                            Console.Out.WriteLine("".PadRightCJK(Math.Min(LINE_COUNT, max_len + 8), '-'));
                            Console.Out.WriteLine($"Total {_flist_out_.Count} Items.");
                        }                            

                        _log_.Add("".PadRightCJK(Math.Min(LINE_COUNT, max_len + 8), '-'));
                        _log_.Add($"Total {_flist_out_.Count} Items.");
                        #endregion

                        #region Save run log to file When applet set output option with filename
                        if (!string.IsNullOrEmpty(applet.ResultFile))
                        {
                            SaveLogToFile(applet.ResultFile);
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
                        var is_contents = result is string && (result as string).StartsWith($"{ImageApplets.Applet.ContentMark}");
                        if (is_contents) result = (result as string).Substring(1).Trim();
                        if (!Console.IsOutputRedirected || applet.Verbose)
                        {
                            if (is_contents)
                                Console.Out.WriteLine($"{fname.PadRightCJK(Math.Max(padding, 1))} : {$"{result}"}");
                            else
                                Console.Out.WriteLine($"{fname.PadRightCJK(Math.Max(padding, 1))} : {($"{result}").PadLeft(5)}");
                        }
                        else
                            Console.Out.WriteLine($"{fname}");

                        if (!(_flist_out_ is List<string>)) _flist_out_ = new List<string>();
                        _flist_out_.Add(fname);

                        if (is_contents)
                            _log_.Add($"{fname.PadRightCJK(Math.Max(padding, 1))} : {$"{result}"}");
                        else
                            _log_.Add($"{fname.PadRightCJK(Math.Max(padding, 1))} : {($"{result}").PadLeft(5)}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is CompactExifLib.ExifException)
                {
                    switch ((ex as CompactExifLib.ExifException).ErrorCode)
                    {
                        case CompactExifLib.ExifErrCode.ImageTypeIsNotSupported: break;
                        default: break;
                    }
                }
            }
        }

        private static void RunApplet(IEnumerable<string> files, ImageApplets.Applet applet, int padding, params string[] extras)
        {
            if (files is IEnumerable<string>)
            {
                foreach (var file in files.Select(f => f.Trim('"')))
                {
                    if (File.Exists(file)) RunApplet(file, applet, padding, extras);
                }
            }
        }

        private static void SaveLogToFile(string file)
        {
            if (!string.IsNullOrEmpty(file))
            {
                if (file.Equals(ImageApplets.Applet.ClipboardName, StringComparison.CurrentCultureIgnoreCase))
                {
                    try
                    {
                        if (_flist_out_.Count > 0)
                        {
                            Thread clip = new Thread(new ThreadStart(delegate()
                            {
                                try
                                {
                                    DataObject dp = new DataObject();
                                    var fdl = new System.Collections.Specialized.StringCollection();
                                    fdl.AddRange(_flist_out_.ToArray());
                                    dp.SetFileDropList(fdl);
                                    dp.SetText(string.Join(Environment.NewLine, _flist_out_));
                                    Clipboard.SetDataObject(dp, true);
                                }
                                catch(Exception ex) { Console.WriteLine(ex.Message); }
                            }));
                            clip.TrySetApartmentState(ApartmentState.STA);
                            clip.Start();
                            clip.Join();
                        }
                    }
                    catch (Exception ex) { Console.WriteLine(ex.Message); }
                }
                else
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
                                if (_kvs_.Count() > 0)
                                {
                                    var _max_key_lens_ = _kvs_.Max(kv => kv.Key.Length);
                                    var _max_value_lens_ = _kvs_.Max(kv => kv.Value.Length);
                                    var _output_lines_ = new List<string> ();
                                    _output_lines_.Add("Result");
                                    _output_lines_.Add("".PadRightCJK(_max_value_lens_ <= 5 ? _max_key_lens_ + 8 : 80, '='));
                                    _output_lines_.AddRange(_kvs_.Select(kv => $"{kv.Key.PadRight(_max_key_lens_)} : {kv.Value.Trim().PadLeft(_max_value_lens_ <= 5 ? 5 : 0)}"));
                                    _output_lines_.Add("".PadRightCJK(_max_value_lens_ <= 5 ? _max_key_lens_ + 8 : 80, '='));
                                    var bytes = Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, _output_lines_).Trim());
                                    fs.Write(bytes, 0, bytes.Length);
                                }
                            }
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
                        sw.WriteLine($"".PadRightCJK(LINE_COUNT, '='));
                        if (string.IsNullOrEmpty(applet_name) || applet_names.Count(a => a.Equals(applet_name, StringComparison.CurrentCultureIgnoreCase)) <= 0)
                        {
                            sw.WriteLine($"Applets:");
                            sw.WriteLine($"".PadRightCJK(LINE_COUNT, '-'));
                            var applets = applet_names.Select(a => ImageApplets.Applet.GetApplet(a)).OrderBy(a => a.Category);
                            foreach (var applet in applets)
                            {
                                if (applet is ImageApplets.Applet)
                                    sw.WriteLine(applet.Help);
                                if (!applet.Name.Equals(applets.Last().Name))
                                    sw.WriteLine($"".PadRightCJK(LINE_COUNT, '-'));
                            }
                        }
                        else
                        {
                            applet_name = applet_names.Where(a => a.Equals(applet_name, StringComparison.CurrentCultureIgnoreCase)).First();
                            var instance = ImageApplets.Applet.GetApplet(applet_name);
                            if (instance is ImageApplets.Applet)
                                sw.WriteLine(instance.Help);
                        }
                        sw.WriteLine($"".PadRightCJK(LINE_COUNT, '='));
                    }
                    result = sw.ToString();
                }
            }
            return (result);
        }
    }

    static public class Extentions
    {
        static private Encoding MBCS = Encoding.GetEncoding("GB18030");

        static private int LengthCJK(string s)
        {
            return (MBCS.GetByteCount(s));
        }

        static public string PadLefttCJK(this string s, int totalWidth)
        {
            return (s.PadLeft(totalWidth - (LengthCJK(s) - s.Length)));
        }

        static public string PadLefttCJK(this string s, int totalWidth, char paddingChar)
        {
            return (s.PadLeft(totalWidth - (LengthCJK(s) - s.Length), paddingChar));
        }

        static public string PadRightCJK(this string s, int totalWidth)
        {
            return (s.PadRight(totalWidth - (LengthCJK(s) - s.Length)));
        }

        static public string PadRightCJK(this string s, int totalWidth, char paddingChar)
        {
            return (s.PadRight(totalWidth - (LengthCJK(s) - s.Length), paddingChar));
        }
    }
}
