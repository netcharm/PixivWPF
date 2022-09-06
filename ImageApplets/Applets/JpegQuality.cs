using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Mono.Options;
using CompactExifLib;

namespace ImageApplets.Applets
{
    class JpegQuality : Applet
    {
        public override Applet GetApplet()
        {
            return (new JpegQuality());
        }

        private int QualityValue = 85;
        private CompareMode Mode = CompareMode.VALUE;
        public JpegQuality()
        {
            var opts = new OptionSet()
            {
                { "m|mode=", "Quality Less Then (<=) {Value}", v => { if (v != null) Enum.TryParse(v.ToUpper(), out Mode); } },
                { "q|quality=", "Quality Equal (=) {Value}", v => { if (v != null) int.TryParse(v, out QualityValue); } },
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

        public override bool Execute<T>(ExifData exif, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                var _QualityValue_ = (args.Length > 0 && args[0] is int) ? (int)args[0] : QualityValue;
                if (exif is ExifData)
                {
                    dynamic status = 0;
                    if (exif.ImageType == ImageType.Jpeg)
                    {
                        var quality = exif.JpegQuality;
                        switch (Mode)
                        {
                            case CompareMode.VALUE: status = quality; break;
                            case CompareMode.NEQ: status = quality != _QualityValue_; break;
                            case CompareMode.EQ: status = quality == _QualityValue_; break;
                            case CompareMode.LT: status = quality < _QualityValue_; break;
                            case CompareMode.LE: status = quality <= _QualityValue_; break;
                            case CompareMode.GT: status = quality > _QualityValue_; break;
                            case CompareMode.GE: status = quality >= _QualityValue_; break;
                            default: break;
                        }
                    }
                    else if (Mode != CompareMode.VALUE) status = false;
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
