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

        private CompareMode _Mode_ = CompareMode.VALUE;
        public CompareMode Mode { get { return (_Mode_); } set { _Mode_ = value; } }
        private int _QualityValue_ = 85;
        public int QualityValue { get { return (_QualityValue_); } set { _QualityValue_ = value; } }
        private double _Tolerance_ = 0.005;
        public double Tolerance { get { return (_Tolerance_); } set { _Tolerance_ = value; } }

        public JpegQuality()
        {
            Category = AppletCategory.ImageAttribure;

            var opts = new OptionSet()
            {
                { "m|mode=", "Quality Comparing Mode {<EQ|NEQ|LT|LE|GT|GE|VALUE>}", v => { if (!string.IsNullOrEmpty(v)) Enum.TryParse(v.ToUpper(), out _Mode_); } },
                { "q|quality=", "Quality Comparing {Value}", v => { if (!string.IsNullOrEmpty(v)) int.TryParse(v, out _QualityValue_); } },
                { "allowance|tolerance=", $"Image Quality Tolerance, default is {_Tolerance_:P}", v => { if (!string.IsNullOrEmpty(v)) double.TryParse(v, out _Tolerance_); } },
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
                var _QualityValue_ = (args.Length > 0 && args[0] is int) ? (int)args[0] : this._QualityValue_;
                if (exif is ExifData)
                {
                    dynamic status = 0;
                    if (exif.ImageType == CompactExifLib.ImageType.Jpeg || exif.ImageType == CompactExifLib.ImageType.Png)
                    {
                        var quality = exif.JpegQuality;
                        if (exif.ImageType == CompactExifLib.ImageType.Jpeg && quality <= 0) quality = 75;
                        else if (exif.ImageType == CompactExifLib.ImageType.Png) quality = 100;

                        switch (_Mode_)
                        {
                            case CompareMode.VALUE: status = quality; break;
                            case CompareMode.NOT:
                            case CompareMode.NEQ: status = quality.Outside(_QualityValue_, _Tolerance_); break;
                            case CompareMode.IS:
                            case CompareMode.EQ: status = quality.Inside(_QualityValue_, _Tolerance_); break;
                            case CompareMode.LT: status = quality < _QualityValue_; break;
                            case CompareMode.LE: status = quality <= _QualityValue_; break;
                            case CompareMode.GT: status = quality > _QualityValue_; break;
                            case CompareMode.GE: status = quality >= _QualityValue_; break;
                            default: break;
                        }
                    }
                    else if (_Mode_ != CompareMode.VALUE) status = false;

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
