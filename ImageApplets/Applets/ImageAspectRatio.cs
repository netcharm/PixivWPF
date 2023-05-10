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
using System.Text.RegularExpressions;

namespace ImageApplets.Applets
{
    class AspectRatio : Applet
    {
        public override Applet GetApplet()
        {
            return (new ImageType());
        }

        private string _Ratio_ = string.Empty;
        public string Ratio { get { return (_Ratio_); } internal set { _Ratio_ = value; } }
        private CompareMode _Mode_ = CompareMode.VALUE;
        public CompareMode Mode { get { return (_Mode_); } internal set { _Mode_ = value; } }
        private double _Tolerance_ = 0.005;

        public AspectRatio()
        {
            Category = AppletCategory.ImageAttribure;

            var opts = new OptionSet()
            {
                { "m|mode=", "Quality Comparing Mode {VALUE} : <IS|NOT|EQ|NEQ|LT|LE|GE|GT|VALUE>", v => { if (v != null) Enum.TryParse(v.ToUpper(), out _Mode_); } },
                //{ "type=", $"Image Type {{{JPG|JPEG|PNG|BMP|TIFF|TIF|ICO|GIF|EMF|WMF}", v => { if (v != null) TypeValue = v; } },
                { "aspect|ratio|type=", $"Image Aspect Ratio {{VALUE}} : <{string.Join("|", GetAspectRatios())}>", v => { if (v != null) _Ratio_ = v; } },
                { "allowance|tolerance=", $"Image Aspect Ratio Tolerance, default is {_Tolerance_:P}", v => { if (v != null) double.TryParse(v, out _Tolerance_); } },
                { "" },
            };
            AppendOptions(opts);
        }

        private string[] GetAspectRatios()
        {
            var result = new List<string>() { "Landscape", "Portrait", "Square", "1:1", "2:1", "1:2", "3:2", "2:3", "4:3", "3:4", "16:9", "9:16", "16:10", "10:16", "21:10", "10:21", "Value" };
            //var result = new List<string>() { "Landscape", "Portrait", "Square", "1^:1", "Value" };
            return (result.ToArray());
        }

        //private bool Inside(double src, Tuple<double, double> dst, double tolerance = 0.005, bool include = true)
        //{
        //    var r_l = dst.Item1;
        //    var r_r = dst.Item2;
        //    if (include)
        //        return (src >= r_l * (1 - tolerance) && src <= r_r * (1 + tolerance));
        //    else
        //        return (src > r_l * (1 - tolerance) && src < r_r * (1 + tolerance));
        //}

        //private bool Outside(double src, Tuple<double, double> dst, double tolerance = 0.005, bool include = false)
        //{
        //    var r_l = dst.Item1;
        //    var r_r = dst.Item2;
        //    if (include)
        //        return (src <= r_l * (1 - tolerance) || src >= r_r * (1 + tolerance));
        //    else
        //        return (src < r_l * (1 - tolerance) || src > r_r * (1 + tolerance));
        //}

        //private bool Inside(double src, double dst, double tolrence = 0.005, bool include = true)
        //{
        //    if (include)
        //        return (src >= dst * (1 - tolrence) && src <= dst * (1 + tolrence));
        //    else
        //        return (src > dst * (1 - tolrence) && src < dst * (1 + tolrence));
        //}

        //private bool Outside(double src, double dst, double tolrence = 0.005, bool include = false)
        //{
        //    if (include)
        //        return (src <= dst * (1 - tolrence) || src >= dst * (1 + tolrence));
        //    else
        //        return (src < dst * (1 - tolrence) || src > dst * (1 + tolrence));
        //}

        public override bool Execute<T>(Stream source, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);

            dynamic status = "Unknown";
            try
            {
                Result.Reset();
                if (source is Stream && source.CanRead)
                {
                    if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                    using (Image image = Image.FromStream(source))
                    {
                        if (!string.IsNullOrEmpty(_Ratio_) && _Mode_ == CompareMode.VALUE) _Mode_ = CompareMode.EQ;

                        dynamic _aspect_ = double.NaN;
                        double aspect = Math.Round((double)(image.Width) / image.Height, 3);

                        if (Regex.IsMatch(_Ratio_, @"^(\d+(\.\d+)?):(\d+(\.\d+)?)$", RegexOptions.IgnoreCase))
                        {
                            var mo = Regex.Match(_Ratio_, @"^(\d+(\.\d+)?):(\d+(\.\d+)?)$", RegexOptions.IgnoreCase);
                            var _w_ = double.Parse(mo.Groups[1].Value);
                            var _h_ = double.Parse(mo.Groups[3].Value);
                            _aspect_ = Math.Round(_w_ / _h_, 3);
                        }
                        else if (_Ratio_.Equals("square", StringComparison.CurrentCultureIgnoreCase)) { _aspect_ = new Tuple<double, double>(1.000, 1.000); }
                        else if (_Ratio_.Equals("landscape", StringComparison.CurrentCultureIgnoreCase)) { _aspect_ = new Tuple<double, double>(1.005, double.PositiveInfinity); }
                        else if (_Ratio_.Equals("portrait", StringComparison.CurrentCultureIgnoreCase)) { _aspect_ = new Tuple<double, double>(0.000, 0.995); }
                        else if (Regex.IsMatch(_Ratio_, @"^(\d+(\.\d+)?)$", RegexOptions.IgnoreCase))
                        {
                            var mo = Regex.Match(_Ratio_, @"^(\d+(\.\d+)?)$", RegexOptions.IgnoreCase);
                            _aspect_ = Math.Round(double.Parse(mo.Groups[1].Value), 3);
                        }

                        switch (_Mode_)
                        {
                            case CompareMode.VALUE: status = $"{aspect:F3}"; break;
                            case CompareMode.NOT:
                            case CompareMode.NEQ:
                                if (_aspect_ is Tuple<double, double>) status = aspect.Outside((Tuple<double, double>)_aspect_, _Tolerance_);
                                else if (!double.IsNaN(_aspect_)) status = aspect.Outside((double)_aspect_, _Tolerance_);
                                else status = false;
                                break;
                            case CompareMode.IS:
                            case CompareMode.EQ:
                                if (_aspect_ is Tuple<double, double>) status = aspect.Inside((Tuple<double, double>)_aspect_, _Tolerance_);
                                else if (!double.IsNaN(_aspect_)) status = aspect.Inside((double)_aspect_, _Tolerance_);
                                else status = false;
                                break;
                            case CompareMode.LT: status = double.IsNaN(_aspect_) ? false : aspect < _aspect_; break;
                            case CompareMode.LE: status = double.IsNaN(_aspect_) ? false : aspect <= _aspect_; break;
                            case CompareMode.GE: status = double.IsNaN(_aspect_) ? false : aspect >= _aspect_; break;
                            case CompareMode.GT: status = double.IsNaN(_aspect_) ? false : aspect > _aspect_; break;
                            default: break;
                        }
                    }
                    ret = GetReturnValueByStatus(status);
                    result = (T)(object)status;
                }
                Result.Set(InputFile, OutputFile, ret, result);
            }
            catch (Exception ex) { if (!(ex.Source.Equals("System.Drawing"))) ShowMessage(ex, Name); }
            return (ret);
        }
    }

    class Aspect : Applet
    {
        private AspectRatio _instance_ { get; set; }

        public override Applet GetApplet()
        {
            return (new Aspect());
        }

        public Aspect()
        {
            var cat = Options[0];
            _instance_ = new AspectRatio();
            Category = _instance_.Category;
            Options = _instance_.Options;
            Options[0] = cat;
        }

        public override bool Execute<T>(Stream source, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);

            dynamic status = "Unknown";
            try
            {
                Result.Reset();
                if (source is Stream && source.CanRead)
                {
                    ret = _instance_.Execute(source, out result, args);
                }
                Result.Set(InputFile, OutputFile, ret, result);
            }
            catch (Exception ex) { if (!(ex.Source.Equals("System.Drawing"))) ShowMessage(ex, Name); }
            return (ret);
        }
    }

    class Ratio : Applet
    {
        private AspectRatio _instance_ { get; set; } = new AspectRatio();

        public override Applet GetApplet()
        {
            return (new Ratio());
        }

        public Ratio()
        {
            var cat = Options[0];
            _instance_ = new AspectRatio();
            Category = _instance_.Category;
            Options = _instance_.Options;
            Options[0] = cat;
        }

        public override bool Execute<T>(Stream source, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);

            dynamic status = "Unknown";
            try
            {
                Result.Reset();
                if (source is Stream && source.CanRead)
                {
                    ret = _instance_.Execute(source, out result, args);
                }
                Result.Set(InputFile, OutputFile, ret, result);
            }
            catch (Exception ex) { if (!(ex.Source.Equals("System.Drawing"))) ShowMessage(ex, Name); }
            return (ret);
        }
    }

    class Square : Applet
    {
        private AspectRatio _instance_ { get; set; } = new AspectRatio();

        public override Applet GetApplet()
        {
            return (new Square());
        }

        public Square()
        {
            var cat = Options[0];
            _instance_ = new AspectRatio();
            Category = _instance_.Category;
            //Options = _instance_.Options;
            //Options[0] = cat;
        }

        public override bool Execute<T>(Stream source, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);

            dynamic status = "Unknown";
            try
            {
                Result.Reset();
                if (source is Stream && source.CanRead)
                {
                    _instance_.Mode = CompareMode.IS;
                    _instance_.Ratio = "Square";
                    ret = _instance_.Execute(source, out result, args);
                }
                Result.Set(InputFile, OutputFile, ret, result);
            }
            catch (Exception ex) { if (!(ex.Source.Equals("System.Drawing"))) ShowMessage(ex, Name); }
            return (ret);
        }
    }

    class Landscape : Applet
    {
        private AspectRatio _instance_ { get; set; } = new AspectRatio();

        public override Applet GetApplet()
        {
            return (new Landscape());
        }

        public Landscape()
        {
            var cat = Options[0];
            _instance_ = new AspectRatio();
            Category = _instance_.Category;
            //Options = _instance_.Options;
            //Options[0] = cat;
        }

        public override bool Execute<T>(Stream source, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);

            dynamic status = "Unknown";
            try
            {
                Result.Reset();
                if (source is Stream && source.CanRead)
                {
                    _instance_.Mode = CompareMode.IS;
                    _instance_.Ratio = "Landscape";
                    ret = _instance_.Execute(source, out result, args);
                }
                Result.Set(InputFile, OutputFile, ret, result);
            }
            catch (Exception ex) { if (!(ex.Source.Equals("System.Drawing"))) ShowMessage(ex, Name); }
            return (ret);
        }
    }

    class Portrait : Applet
    {
        private AspectRatio _instance_ { get; set; } = new AspectRatio();

        public override Applet GetApplet()
        {
            return (new Portrait());
        }

        public Portrait()
        {
            var cat = Options[0];
            _instance_ = new AspectRatio();
            Category = _instance_.Category;
            //Options = _instance_.Options;
            //Options[0] = cat;
        }

        public override bool Execute<T>(Stream source, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);

            dynamic status = "Unknown";
            try
            {
                Result.Reset();
                if (source is Stream && source.CanRead)
                {
                    _instance_.Mode = CompareMode.IS;
                    _instance_.Ratio = "Portrait";
                    ret = _instance_.Execute(source, out result, args);
                }
                Result.Set(InputFile, OutputFile, ret, result);
            }
            catch (Exception ex) { if (!(ex.Source.Equals("System.Drawing"))) ShowMessage(ex, Name); }
            return (ret);
        }
    }

}
