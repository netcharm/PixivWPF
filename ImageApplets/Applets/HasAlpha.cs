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
            return (new HasExif());
        }

        private int WindowSize = 3;
        public HasAlpha()
        {
            var opts = new OptionSet()
            {
                { "m|w|matrix|window=", "Matrix Window {SIZE}", v => { if (v != null) int.TryParse(v, out WindowSize); } },
            };

            foreach (var opt in opts.Reverse())
            {
                try
                {
                    Options.Insert(1, opt);
                }
                catch (Exception ex) { ShowMessage(ex); }
            }
        }

        private Color[] GetMatrix(Bitmap bmp, int x, int y, int w, int h)
        {
            var ret = new List<Color>();
            if (bmp is Bitmap)
            {
                for (var i = x; i < x + w; i++)
                {
                    for (var j = y; j < y + h; j++)
                    {
                        if (i < bmp.Width && j < bmp.Height)
                            ret.Add(bmp.GetPixel(i, j));
                    }
                }
            }
            return (ret.ToArray());
        }

        public override bool Execute<T>(Stream source, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                var _WindowSize_ = (args.Length > 0 && args[0] is int) ? (int)args[0] : WindowSize;
                if (source is Stream && source.CanRead)
                {
                    var status = false;
                    if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                    Image image = Image.FromStream(source);
                    if (image is Image && image.RawFormat.Guid.Equals(ImageFormat.Png.Guid))
                    {
                        if (image.PixelFormat == PixelFormat.Format32bppArgb) { status = true; }
                        else if (image.PixelFormat == PixelFormat.Format32bppPArgb) { status = true; }
                        else if (image.PixelFormat == PixelFormat.Format16bppArgb1555) { status = true; }
                        else if (image.PixelFormat == PixelFormat.Format64bppArgb) { status = true; }
                        else if (image.PixelFormat == PixelFormat.Format64bppPArgb) { status = true; }
                        else if (image.PixelFormat == PixelFormat.PAlpha) { status = true; }
                        else if (image.PixelFormat == PixelFormat.Alpha) { status = true; }
                        else if (Image.IsAlphaPixelFormat(image.PixelFormat)) { status = true; }

                        if (status)
                        {
                            var bmp = new Bitmap(image);
                            var w = bmp.Width;
                            var h = bmp.Height;
                            var m = _WindowSize_;
                            var mt = Math.Ceiling(m * m / 2.0);
                            var lt = GetMatrix(bmp, 0, 0, m, m).Count(c => c.A < 255);
                            var rt = GetMatrix(bmp, w - m, 0, m, m).Count(c => c.A < 255);
                            var lb = GetMatrix(bmp, 0, h - m, m, m).Count(c => c.A < 255);
                            var rb = GetMatrix(bmp, w - m, h - m, m, m).Count(c => c.A < 255);
                            status = (lt > mt || rt > mt || lb > mt || rb > mt) ? true : false;
                        }
                    }
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
                    result = (T)(object)status;
                }
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }
    }
}
