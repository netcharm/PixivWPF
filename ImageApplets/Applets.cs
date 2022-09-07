using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

using Mono.Options;
using CompactExifLib;

namespace ImageApplets
{
    public enum CompareMode { EQ, NEQ, GT, LT, GE, LE, HAS, NONE, AND, OR, NOT, XOR, BEFORE, PREV, TODAY, NEXT, AFTER, VALUE };
    public enum DateCompareMode { BEFORE, PREV, TODAY, NEXT, AFTER };
    public enum DateUnit { DAY, WEEK, MONTH, SEASON, YEAR };

    public enum AppletCategory { FileOP, ImageType, ImageContent, ImageAttribure, Other, Unknown, None }
    public enum STATUS { All, Yes, No, None };
    public enum ReadMode { ALL, LINE };

    public interface IApplet
    {
        Applet GetApplet();
    }

    public abstract class Applet: IApplet
    {

        private static bool show_help = false;
        static protected STATUS Status { get; set; } = STATUS.All;

        public virtual AppletCategory Category { get; internal protected set; } = AppletCategory.Unknown;

        static private ReadMode _ReadInputMode_ = ReadMode.LINE;
        public ReadMode ReadInputMode { get { return (_ReadInputMode_); } }

        static public string[] LINE_BREAK { get; set; } = new string[] { Environment.NewLine, "\n\r", "\r\n", "\n", "\r" };

        public string Name { get { return (this.GetType().Name); } }

        public int ValuePaddingLeft { get; set; } = 0;
        public int ValuePaddingRight { get; set; } = 0;

        public virtual OptionSet Options { get; set; } = new OptionSet()
        {
            { "t|y|true|yes", "Keep True Result", v => { Status = STATUS.Yes; } },
            { "f|n|false|no", "Keep False Result", v => { Status = STATUS.No; } },
            { "a|all", "Keep All", v => { Status = STATUS.All; } },
            { " " },
            { "filelist=", "Get Files From {FILE}", v => { if (v != null) Enum.TryParse(v.ToUpper(), out _ReadInputMode_); } },
            { "read=", "Read Mode {<All|Line>} When Input Redirected", v => { if (v != null) Enum.TryParse(v.ToUpper(), out _ReadInputMode_); } },
        };

        internal protected virtual void AppendOptions(OptionSet opts, int index = 1)
        {
            foreach (var opt in opts.Reverse())
            {
                try
                {
                    Options.Insert(index, opt);
                }
                catch (Exception ex) { ShowMessage(ex); }
            }
        }

        public Applet()
        {
            var opts = new OptionSet()
            {
                { $"{Name}" },
            };

            AppendOptions(opts, 0);
        }

        static public IEnumerable<string> GetApplets()
        {
            var result = new List<string>();
            Assembly applets = typeof(Applet).Assembly;
            foreach (Type type in applets.GetTypes())
            {
                if (type.BaseType == typeof(Applet))
                    result.Add(type.Name);
            }
            return (result);
        }

        public abstract Applet GetApplet();

        static public Applet GetApplet(string applet = null)
        {
            Applet result = null;
            if (string.IsNullOrEmpty(applet))
            {
                //result = new Applet();
            }
            else
            {
                Assembly applets = typeof(Applet).Assembly;
                foreach (Type type in applets.GetTypes())
                {
                    if (type.BaseType == typeof(Applet) && type.Name.Equals(applet, StringComparison.CurrentCultureIgnoreCase))
                    {
                        result = Assembly.GetAssembly(type).CreateInstance(type.FullName) as Applet;
                    }
                }
            }
            return (result);
        }

        public virtual void ShowMessage(string text, string title = null)
        {
            if (string.IsNullOrEmpty(title))
                MessageBox.Show(text);
            else
                MessageBox.Show(text, title);
        }

        public virtual void ShowMessage(Exception ex, string title = null)
        {
            if (string.IsNullOrEmpty(title))
                MessageBox.Show($"{ex.Message}{Environment.NewLine}{ex.StackTrace}");
            else
                MessageBox.Show($"{ex.Message}{Environment.NewLine}{ex.StackTrace}", title);
        }

        public virtual string Help(string indent = null)
        {
            var result = string.Empty;
            if (Options is OptionSet)
            {
                using (var sw = new StringWriter())
                {
                    if(base.GetType().BaseType == typeof(Applet))
                        Options.WriteOptionDescriptions(sw);
                    result = string.Join(Environment.NewLine, sw.ToString().Trim().Split(LINE_BREAK, StringSplitOptions.None).Select(l => $"{indent}{l}"));
                }
            }
            return (result);
        }

        public virtual void PharseOptions(IEnumerable<string> args)
        {
            var extras = Options.Parse(args);
        }

        public virtual bool GetReturnValueByStatus(dynamic status)
        {
            var ret = true;
            if (status is bool)
            {
                switch (Status)
                {
                    case STATUS.Yes:
                        ret = status;
                        break;
                    case STATUS.No:
                        ret = !status;
                        break;
                    default:
                        ret = true;
                        break;
                }
            }
            return (ret);
        }

        public virtual bool Execute<T>(string file, out T result, params object[] args)
        {
            bool ret = false;
            result = default(T);
            if (File.Exists(file))
            {
                try
                {
                    var fi = new FileInfo(file);
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        ret = Execute<T>(fs, out result);
                    }
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            return (ret);
        }

        public virtual bool Execute<T>(Stream source, out T result, params object[] args)
        {
            bool ret = false;
            result = default(T);
            if (source is Stream && source.CanRead)
            {
                try
                {
                    if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                    var exif = new ExifData(source);
                    ret = Execute<T>(exif, out result);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            return (ret);
        }

        public virtual bool Execute<T>(ExifData exif, out T result, params object[] args)
        {
            bool ret = false;
            result = default(T);
            try
            {
                ret = true;
            }
            catch(Exception ex) { MessageBox.Show(ex.Message); }
            return (ret);
        }

        public virtual IEnumerable<FileInfo> Execute(IEnumerable<FileInfo> files, STATUS status = STATUS.Yes, params object[] args)
        {
            Status = status;
            var infolist = new List<FileInfo>();
            foreach (var file in files)
            {
                bool result = false;
                var ret = Execute(file.FullName, out result, args);
                if (ret) infolist.Add(file);
            }
            return (infolist);
        }

        public virtual IEnumerable<string> Execute(IEnumerable<string> files, STATUS status = STATUS.Yes, params object[] args)
        {
            Status = status;
            var infolist = new List<string>();
            foreach (var file in files)
            {
                bool result = false;
                var ret = Execute(file, out result, args);
                if (ret) infolist.Add(file);
            }
            return (infolist);
        }
    }
}
