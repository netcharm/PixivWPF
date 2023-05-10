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
    class ImageType : Applet
    {
        public override Applet GetApplet()
        {
            return (new ImageType());
        }

        private string TypeValue = string.Empty;
        private CompareMode Mode = CompareMode.VALUE;

        public ImageType()
        {
            Category = AppletCategory.ImageType;

            var opts = new OptionSet()
            {
                { "m|mode=", "Quality Comparing Mode {{VALUE}} : <IS|NOT|EQ|NEQ|VALUE>", v => { if (v != null) Enum.TryParse(v.ToUpper(), out Mode); } },
                //{ "type=", $"Image Type {{{JPG|JPEG|PNG|BMP|TIFF|TIF|ICO|GIF|EMF|WMF}", v => { if (v != null) TypeValue = v; } },
                { "type=", $"Image Type {{VALUE}} : <{string.Join("|", GetImageTypeNames())}>", v => { if (v != null) TypeValue = v; } },
                { "" },
            };
            AppendOptions(opts);
        }

        private string[] GetImageTypeNames()
        {
            var result = new List<string>() { "JPG", "TIF", "PNG", "BMP" };
            var codecs = ImageCodecInfo.GetImageDecoders();
            result.AddRange(codecs.Select(c => c.FormatDescription.ToUpper()));
            result = result.OrderBy(c => c).Distinct().ToList();
            return (result.ToArray());
        }

        private string GetImageTypeName(Guid guid)
        {
            var result = string.Empty;
            var codecs = ImageCodecInfo.GetImageDecoders();
            foreach (var codec in codecs)
            {
                if (codec.FormatID.Equals(guid)) { result = codec.FormatDescription; break; }
            }
            return (result);
        }

        public override bool Execute<T>(Stream source, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);

            var ext = source is FileStream ? Path.GetExtension((source as FileStream).Name) : "Unknown";
            dynamic status = ext;
            try
            {
                Result.Reset();
                if (source is Stream && source.CanRead)
                {
                    if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                    using (Image image = Image.FromStream(source))
                    {
                        if (!string.IsNullOrEmpty(TypeValue) && Mode == CompareMode.VALUE) Mode = CompareMode.EQ;

                        var _TypeValue_ = (args.Length > 0 && args[0] is string) ? (string)args[0] : TypeValue;
                        if (string.IsNullOrEmpty(_TypeValue_)) _TypeValue_ = status;
                        else if (_TypeValue_.Equals("jpg", StringComparison.CurrentCultureIgnoreCase)) _TypeValue_ = "Jpeg";
                        else if (_TypeValue_.Equals("tif", StringComparison.CurrentCultureIgnoreCase)) _TypeValue_ = "Tiff";

                        var typename = GetImageTypeName(image.RawFormat.Guid);
                        switch (Mode)
                        {
                            case CompareMode.VALUE: status = typename; break;
                            case CompareMode.NOT:
                            case CompareMode.NEQ: status = !typename.Equals(_TypeValue_, StringComparison.CurrentCultureIgnoreCase); break;
                            case CompareMode.IS:
                            case CompareMode.EQ: status = typename.Equals(_TypeValue_, StringComparison.CurrentCultureIgnoreCase); break;
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
}
