using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Mono.Options;
using CompactExifLib;

namespace ImageApplets.Applets
{
    class HasAlpha : Applet
    {
        public override Applet GetApplet()
        {
            return (new HasAlpha());
        }

        private int _WindowSize_ = 3;
        public int WindowSize { get { return (_WindowSize_); } set { _WindowSize_ = value; } }
        private int _Threshold_ = 255;
        public int Threshold { get { return (_Threshold_); } set { _Threshold_ = value; } }


        public HasAlpha()
        {
            Category = AppletCategory.ImageContent;

            var opts = new OptionSet()
            {
                { "m|w|matrix|window=", "Matrix Window {Size}", v => { if (!string.IsNullOrEmpty(v)) int.TryParse(v, out _WindowSize_); } },
                { "v|threshold=", "Threshold {VALUE}", v => { if (!string.IsNullOrEmpty(v)) int.TryParse(v, out _Threshold_); } },
                { "" },
            };
            AppendOptions(opts);
        }

        public override bool Execute<T>(Stream source, out T result, params object[] args)
        {
            var ret = false;
            result = default;
            try
            {
                Result.Reset();
                var _WindowSize_ = (args.Length > 0 && args[0] is int) ? (int)args[0] : this._WindowSize_;
                if (source is Stream && source.CanRead)
                {
                    var status = false;
                    if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                    using (Image image = Image.FromStream(source))
                    {
                        status = image.GuessAlpha(_WindowSize_, _Threshold_);
                    }
                    ret = GetReturnValueByStatus(status);
                    result = (T)(object)status;
                }
                Result.Set(InputFile, OutputFile, ret, result);
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }
    }

    class NoAlpha : Applet
    {
        public override Applet GetApplet()
        {
            return (new NoAlpha());
        }

        private int _WindowSize_ = 3;
        public int WindowSize { get { return (_WindowSize_); } set { _WindowSize_ = value; } }
        private int _Threshold_ = 255;
        public int Threshold { get { return (_Threshold_); } set { _Threshold_ = value; } }


        public NoAlpha()
        {
            Category = AppletCategory.ImageContent;

            var opts = new OptionSet()
            {
                { "m|w|matrix|window=", "Matrix Window {Size}", v => { if (!string.IsNullOrEmpty(v)) int.TryParse(v, out _WindowSize_); } },
                { "v|threshold=", "Threshold {VALUE}", v => { if (!string.IsNullOrEmpty(v)) int.TryParse(v, out _Threshold_); } },
                { "" },
            };
            AppendOptions(opts);
        }

        public override bool Execute<T>(Stream source, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                Result.Reset();
                var _WindowSize_ = (args.Length > 0 && args[0] is int) ? (int)args[0] : this._WindowSize_;
                if (source is Stream && source.CanRead)
                {
                    var status = false;
                    if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                    using (Image image = Image.FromStream(source))
                    {
                        status = !image.GuessAlpha(_WindowSize_, _Threshold_);
                    }
                    ret = GetReturnValueByStatus(status);
                    result = (T)(object)status;
                }
                Result.Set(InputFile, OutputFile, ret, result);
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }
    }
}
