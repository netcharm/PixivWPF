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
            Category = AppletCategory.ImageAttribure;

            var opts = new OptionSet()
            {
                { "m|mode=", "Quality Comparing Mode {<EQ|NEQ|LT|LE|GT|GE|VALUE>}", v => { if (v != null) Enum.TryParse(v.ToUpper(), out Mode); } },
                { "q|quality=", "Quality Comparing {Value}", v => { if (v != null) int.TryParse(v, out QualityValue); } },
                { "" },
            };
            AppendOptions(opts);
        }

        public override bool Execute<T>(ExifData exif, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                Result.Reset();
                var _QualityValue_ = (args.Length > 0 && args[0] is int) ? (int)args[0] : QualityValue;
                if (exif is ExifData)
                {
                    dynamic status = 0;
                    if (exif.ImageType == CompactExifLib.ImageType.Jpeg)
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
