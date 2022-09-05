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
    public enum STATUS { All, Yes, No, None };

    public interface IApplet
    {
        Applet GetApplet();
    }

    public abstract class Applet: IApplet
    {
        private static bool show_help = false;
        static protected STATUS Status { get; set; } = STATUS.All;

        static public string[] LINE_BREAK { get; set; } = new string[] { Environment.NewLine, "\n\r", "\r\n", "\n", "\r" };

        public string Name { get { return (this.GetType().Name); } }

        public virtual OptionSet Options { get; set; } = new OptionSet()
        {
            { "t|y|true|yes", "Keep True Result", v => { Status = STATUS.Yes; } },
            { "f|n|false|no", "Keep False Result", v => { Status = STATUS.No; } },
            { "a|all", "Keep All", v => { Status = STATUS.All; } },
        };

        public Applet()
        {
            var opts = new OptionSet()
            {
                { $"{Name}" },
            };

            foreach (var opt in opts.Reverse())
            {
                try
                {
                    Options.Insert(0, opt);
                }
                catch (Exception ex) { ShowMessage(ex); }
            }
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
                    result = string.Join(Environment.NewLine, sw.ToString().Split(LINE_BREAK, StringSplitOptions.None).Select(l => $"{indent}{l}"));
                }
            }
            return (result);
        }

        public virtual void PharseOptions(IEnumerable<string> args)
        {
            var extras = Options.Parse(args);
        }

        public virtual bool Execute<T>(string file, out T result, params object[] args)
        {
            bool ret = false;
            result = default(T);
            if (File.Exists(file))
            {
                try
                {
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
    }
}
