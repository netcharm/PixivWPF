using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

//using Microsoft.Win32;
using Mono.Options;
using CompactExifLib;

namespace ImageApplets.Applets
{
    class Convert : Applet
    {
        public override Applet GetApplet()
        {
            return (new Convert());
        }

        private string _TargetFolder_ = string.Empty;
        public string TargetFolder { get { return (_TargetFolder_); } set { _TargetFolder_ = value; } }
        private bool _OverWrite_ = false;
        public bool OverWrite { get { return (_OverWrite_); } set { _OverWrite_ = value; } }
        private bool _TargetName_ = false;
        public bool TargetName { get { return (_TargetName_); } set { _TargetName_ = value; } }

        private string[] _supported_format_ = new string[] { "jpg", "jpeg", "png", "bmp", "gif", "tif", "tiff" };
        private string _ImageFormat_ = string.Empty;
        public string ImageFormat { get { return (_ImageFormat_); } set { _ImageFormat_ = value; } }
        private int _ImageQuality_ = 85;
        public int ImageQuality { get { return (_ImageQuality_); } set { _ImageQuality_ = value; } }
        private bool _KeepName_ = false;
        public bool KeepName { get { return (_KeepName_); } set { _KeepName_ = value; } }

        //private int ConvertQuality = Properties.Settings.Default.ConvertQuality;
        //private int ReduceQuality = Properties.Settings.Default.ReduceQuality;
        private Color _BGColor_ = Colors.Gray;
        public Color ConvertBGColor { get { return (_BGColor_); } set { _BGColor_ = value; } }

        private string[] LineBreak = new string[] { Environment.NewLine, "\r\n", "\n\r", "\n", "\r" };
        private string[] png_meta_chunk_text = new string[]{ "iTXt", "tEXt", "zTXt" };

        public Convert()
        {
            Category = AppletCategory.FileOP;

            var opts = new OptionSet()
            {
                { "d|folder=", "Target {Folder}", v => { _TargetFolder_ = !string.IsNullOrEmpty(v) ? v : "."; } },
                { "o|overwrite", "Overwrite Exists File", v => { _OverWrite_ = true; } },
                { "s|showtarget", "Out Target File Name", v => { _TargetName_ = true; } },
                { "format=", "Out Image Format, {Format}:<JPG|JPEG|PNG|BMP|GIF|TIF|TIFF>, default is JPG", v => { _ImageFormat_ = string.IsNullOrEmpty(v) ? string.Empty : v.ToLower().Trim('.'); } },
                { "q|quality=", "Out Image Quality, default is 85", v => { if (!string.IsNullOrEmpty(v)) int.TryParse(v, out _ImageQuality_); } },
                { "k|keepname", "Keep Out Image File Name", v => { _KeepName_ = true; } },
                { "bg|back|bgcolor=", "Set Out Image Background Color, default is Gray", v => 
                    {
                        if (!string.IsNullOrEmpty(v))
                        {
                            try
                            {
                                v = v.Trim('#');
                                if (Regex.IsMatch(v, @"^\#?(([0-9a-f]{2}){2,4}|[0-9a-f]{3})$", RegexOptions.IgnoreCase))
                                    _BGColor_ = (Color)(ColorConverter.ConvertFromString($"#{v}"));
                                else
                                    _BGColor_ = (Color)(ColorConverter.ConvertFromString(v));
                            }
                            catch(Exception ex) { ShowMessage(ex.Message); }
                        }
                    }
                },
                { "" },
            };
            AppendOptions(opts);
        }

        private string DecompressText(byte[] bytes, Encoding encoding = default(Encoding), int skip = 2)
        {
            var result = string.Empty;
            try
            {
                if (encoding == default(Encoding)) encoding = Encoding.UTF8;
                using (var msi = new MemoryStream(bytes))
                {
                    using (var mso = new MemoryStream())
                    {
#if DEBUG
                        var zso = new MemoryStream();
                        //var zs = new Hjg.Pngcs.Zlib.AZlibInputStream(msi, false);
                        var zsi = Hjg.Pngcs.Zlib.AZlibInputStream.Null;
                        zsi.Write(bytes, 0, bytes.Length);
                        zsi.CopyTo(zso);
                        zsi.Close();
                        zso.ToArray();
                        result = encoding.GetString(zso.ToArray());
#endif
                        ///
                        /// below lines is need in VS2015 (C# 6.0) must add a gzip header struct & skip two bytes of zlib header, 
                        /// VX2017 (c# 7.0+) work fina willout this lines
                        ///
                        ////private static int GZIP_MAGIC = 35615;
                        //private static byte[] GZIP_MAGIC_HEADER = new byte[] { 0x1F, 0x8B, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };                        
                        //if (bytes[0] != GZIP_MAGIC_HEADER[0] && bytes[1] != GZIP_MAGIC_HEADER[1]) bytes = GZIP_MAGIC_HEADER.Concat(bytes.Skip(2)).ToArray();
                        using (var ds = new System.IO.Compression.GZipStream(msi, System.IO.Compression.CompressionMode.Decompress))
                        {
                            ds.CopyTo(mso);
                            ds.Close();
                        }
                        var ret = mso.ToArray();
                        try
                        {
                            var text = string.Join("", encoding.GetString(ret).Trim().Split(LineBreak, StringSplitOptions.RemoveEmptyEntries).Skip(2));
                            var buff = new byte[text.Length/2];
                            for (var i = 0; i < text.Length / 2; i++)
                            {
                                buff[i] = System.Convert.ToByte($"0x{text[2 * i]}{text[2 * i + 1]}", 16);
                            }
                            //result = encoding.GetString(buff.Skip(skip).ToArray());
                            result = encoding.GetString(buff);
                        }
                        catch (Exception ex) { ShowMessage(ex.Message); };
                    }
                }
            }
            catch (Exception ex) { ShowMessage(ex.Message); }
            return (result);
        }

        private Dictionary<string, string> GetPngMetaInfo(Stream src, Encoding encoding = default(Encoding), bool full_field = true)
        {
            var result = new Dictionary<string, string>();
            try
            {
                if (src is Stream && src.CanRead && src.Length > 0)
                {
                    if (encoding == default(Encoding)) encoding = Encoding.UTF8;
                    var png_r  = new Hjg.Pngcs.PngReader(src);
                    if (png_r is Hjg.Pngcs.PngReader)
                    {
                        png_r.ChunkLoadBehaviour = Hjg.Pngcs.Chunks.ChunkLoadBehaviour.LOAD_CHUNK_ALWAYS;
                        var png_chunks = png_r.GetChunksList();
                        foreach (var chunk in png_chunks.GetChunks())
                        {
                            if (png_meta_chunk_text.Contains(chunk.Id))
                            {
                                var raw = chunk.CreateRawChunk();
                                chunk.ParseFromRaw(raw);

                                var data = encoding.GetString(raw.Data).Split('\0');
                                var key = data.FirstOrDefault();
                                var value = string.Empty;
                                if (chunk.Id.Equals("zTXt"))
                                {
                                    value = DecompressText(raw.Data.Skip(key.Length + 2).SkipWhile(c => c == 0).ToArray());
                                    if ((raw.Data.Length > key.Length + 2) && string.IsNullOrEmpty(value)) value = "(Decodeing Error)";
                                }
                                else if (chunk.Id.Equals("iTXt"))
                                {
                                    var vs = raw.Data.Skip(key.Length + 1).ToArray();
                                    var compress_flag = vs[0];
                                    var compress_method = vs[1];
                                    var language_tag = string.Empty;
                                    var translate_tag = string.Empty;
                                    var text = string.Empty;

                                    if (vs[2] == 0 && vs[3] == 0)
                                        text = compress_flag == 1 ? DecompressText(vs.SkipWhile(c => c == 0).ToArray()) : encoding.GetString(vs.SkipWhile(c => c == 0).ToArray());
                                    else if (vs[2] == 0 && vs[3] != 0)
                                    {
                                        var trans = vs.Skip(3).TakeWhile(c => c != 0);
                                        translate_tag = encoding.GetString(trans.ToArray());

                                        var txt = vs.Skip(3).Skip(trans.Count()).SkipWhile(c => c == 0);
                                        text = compress_flag == 1 ? DecompressText(txt.SkipWhile(c => c == 0).ToArray()) : encoding.GetString(txt.ToArray());
                                    }

                                    value = full_field ? $"{(int)compress_flag}, {(int)compress_method}, {language_tag}, {translate_tag}, {text}" : text.Trim().Trim('\0');
                                }
                                else
                                    value = full_field ? string.Join(", ", data.Skip(1)) : data.Last().Trim().Trim('\0');

                                result[key] = value;
                            }
                        }
                        png_r.End();
                    }
                }
            }
            catch (Exception ex) { ShowMessage(ex.Message); }
            return (result);
        }

        private Dictionary<string, string> GetPngMetaInfo(System.IO.FileInfo fileinfo, Encoding encoding = default(Encoding), bool full_field = true)
        {
            var result = new Dictionary<string, string>();
            try
            {
                if (fileinfo.Exists && fileinfo.Length > 0)
                {
                    using (var msi = new MemoryStream(File.ReadAllBytes(fileinfo.FullName)))
                    {
                        result = GetPngMetaInfo(msi, encoding, full_field);
                    }
                }
            }
            catch (Exception ex) { ShowMessage(ex.Message); }
            return (result);
        }

        private System.Drawing.Imaging.ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            // Get image codecs for all image formats 
            var codecs = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders();

            // Find the correct image codec 
            for (int i = 0; i < codecs.Length; i++)
            {
                if (codecs[i].MimeType == mimeType) return codecs[i];
            }

            return null;
        }

        private byte[] ConvertImageTo(byte[] buffer, string fmt, int quality = 85)
        {
            byte[] result = null;
            try
            {
                if (buffer is byte[] && buffer.Length > 0)
                {
                    System.Drawing.Imaging.ImageFormat pFmt = System.Drawing.Imaging.ImageFormat.MemoryBmp;

                    fmt = fmt.ToLower();
                    if (fmt.Equals("png")) pFmt = System.Drawing.Imaging.ImageFormat.Png;
                    else if (fmt.Equals("jpg") || fmt.Equals("jpeg")) pFmt = System.Drawing.Imaging.ImageFormat.Jpeg;
                    else if (fmt.Equals("tif") || fmt.Equals("tiff")) pFmt = System.Drawing.Imaging.ImageFormat.Tiff;
                    else if (fmt.Equals("bmp")) pFmt = System.Drawing.Imaging.ImageFormat.Bmp;
                    else if (fmt.Equals("gif")) pFmt = System.Drawing.Imaging.ImageFormat.Gif;
                    else return (buffer);

                    if (quality <= 0 || quality > 100) quality = 85;

                    using (var mi = new MemoryStream(buffer))
                    {
                        using (var mo = new MemoryStream())
                        {
                            var bmp = new System.Drawing.Bitmap(mi);
                            var codec_info = GetEncoderInfo("image/jpeg");
                            var qualityParam = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                            var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
                            encoderParams.Param[0] = qualityParam;
                            if (pFmt == System.Drawing.Imaging.ImageFormat.Jpeg)
                            {
                                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                                {
                                    if (mi.CanSeek) mi.Seek(0, SeekOrigin.Begin);
                                    var img = new System.Drawing.Bitmap(mi);
                                    var bg = _BGColor_;
                                    g.Clear(System.Drawing.Color.FromArgb(bg.A, bg.R, bg.G, bg.B));
                                    g.DrawImage(img, 0, 0, new System.Drawing.Rectangle(new System.Drawing.Point(), bmp.Size), System.Drawing.GraphicsUnit.Pixel);
                                    img.Dispose();
                                }
                                bmp.Save(mo, codec_info, encoderParams);
                            }
                            else
                                bmp.Save(mo, pFmt);
                            result = mo.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex) { ShowMessage(ex.Message); }
            return (result);
        }

        private Stream ConvertImageTo(Stream src, string fmt, int quality = 75, bool keep_name = false)
        {
            Stream result = null;
            try
            {
                if (src is Stream && src.CanRead)
                {
                    var pos = src.Position;
                    if (src.CanSeek) src.Seek(0, SeekOrigin.Begin);
                    using (var msi = new MemoryStream((int)(src.Length)))
                    {
                        src.CopyTo(msi);
                        if (src.CanSeek) src.Seek(pos, SeekOrigin.Begin);

                        DateTime dt;
                        var exif = new ExifData(msi);
                        if (exif.ImageType == CompactExifLib.ImageType.Png)
                        {
                            msi.Seek(0, SeekOrigin.Begin);
                            var meta = GetPngMetaInfo(msi, full_field: false);
                            if (meta.ContainsKey("Creation Time") && DateTime.TryParse(Regex.Replace(meta["Creation Time"], @"^(\d{4}):(\d{2})\:(\d{2})(.*?)$", "$1/$2/$3T$4", RegexOptions.IgnoreCase), out dt))
                            {
                                if (!exif.TagExists(ExifTag.DateTime)) exif.SetDateChanged(dt);
                                if (!exif.TagExists(ExifTag.DateTimeDigitized)) exif.SetDateDigitized(dt);
                                if (!exif.TagExists(ExifTag.DateTimeOriginal)) exif.SetDateTaken(dt);
                            }
                            if (!exif.TagExists(ExifTag.XpTitle) && meta.ContainsKey("Title"))
                                exif.SetTagValue(ExifTag.XpTitle, meta["Title"], StrCoding.Utf16Le_Byte);
                            if (!exif.TagExists(ExifTag.XpSubject) && meta.ContainsKey("Subject"))
                                exif.SetTagValue(ExifTag.XpSubject, meta["Subject"], StrCoding.Utf16Le_Byte);
                            //exif.SetTagRawData(ExifTag.XpSubject, ExifTagType.Byte, Encoding.Unicode.GetByteCount(meta["Subject"]), Encoding.Unicode.GetBytes(meta["Subject"]));
                            if (!exif.TagExists(ExifTag.XpAuthor) && meta.ContainsKey("Author"))
                            {
                                exif.SetTagValue(ExifTag.Artist, meta["Author"], StrCoding.Utf8);
                                exif.SetTagValue(ExifTag.XpAuthor, meta["Author"], StrCoding.Utf16Le_Byte);
                                //exif.SetTagRawData(ExifTag.XpAuthor, ExifTagType.Byte, Encoding.Unicode.GetByteCount(meta["Author"]), Encoding.Unicode.GetBytes((meta["Author"]));
                            }
                            if (!exif.TagExists(ExifTag.Copyright) && meta.ContainsKey("Copyright"))
                                exif.SetTagValue(ExifTag.Copyright, meta["Copyright"], StrCoding.Utf8);

                            if (!exif.TagExists(ExifTag.XpComment) && meta.ContainsKey("Description"))
                                exif.SetTagValue(ExifTag.XpComment, meta["Description"], StrCoding.Utf16Le_Byte);
                            if (!exif.TagExists(ExifTag.UserComment) && meta.ContainsKey("Description"))
                                exif.SetTagValue(ExifTag.UserComment, meta["Description"], StrCoding.IdCode_Utf16);

                            if (!exif.TagExists(ExifTag.XpKeywords) && meta.ContainsKey("Comment"))
                                exif.SetTagValue(ExifTag.XpKeywords, meta["Comment"], StrCoding.Utf16Le_Byte);

                            if (!exif.TagExists(ExifTag.FileSource) && meta.ContainsKey("Source"))
                                exif.SetTagValue(ExifTag.FileSource, meta["Source"], StrCoding.Utf8);

                            if (!exif.TagExists(ExifTag.Software) && meta.ContainsKey("Software"))
                                exif.SetTagValue(ExifTag.Software, meta["Software"], StrCoding.Utf8);

                            if (!exif.TagExists(ExifTag.XmpMetadata) && meta.ContainsKey("XML:com.adobe.xmp"))
                            {
                                var value = string.Join("", meta["XML:com.adobe.xmp"].Split(new char[]{ '\0' }).Last().ToArray().SkipWhile(c => c != '<'));
                                exif.SetTagRawData(ExifTag.XmpMetadata, ExifTagType.Byte, Encoding.UTF8.GetByteCount(value), Encoding.UTF8.GetBytes(value));
                            }
                            else if (!exif.TagExists(ExifTag.XmpMetadata) && meta.ContainsKey("Raw profile type xmp"))
                            {
                                var value = string.Join("", meta["Raw profile type xmp"].Split(new char[]{ '\0' }).Last().ToArray().SkipWhile(c => c != '<'));
                                exif.SetTagRawData(ExifTag.XmpMetadata, ExifTagType.Byte, Encoding.UTF8.GetByteCount(value), Encoding.UTF8.GetBytes(value));
                            }
                        }

                        var bo = ConvertImageTo(msi.ToArray(), fmt, quality: quality);
                        using (var msp = new MemoryStream(bo))
                        {
                            var exif_out = new ExifData(msp);
                            exif_out.ReplaceAllTagsBy(exif);
                            result = new MemoryStream();
                            msp.Seek(0, SeekOrigin.Begin);
                            exif_out.Save(msp, result);
                        }
                    }
                }
            }
            catch (Exception ex) { ShowMessage(ex.Message); }
            return (result);
        }

        private Stream ReduceImageFileSize(Stream src, string fmt, int quality = 75, bool keep_name = true)
        {
            return (ConvertImageTo(src, fmt, quality, keep_name));
        }

        private string ConvertImageTo(string src, string dst, string fmt, int quality = 75, bool keep_name = false)
        {
            string result = string.Empty;
            try
            {
                if (!string.IsNullOrEmpty(src) && File.Exists(src))
                {
                    var fi = new System.IO.FileInfo(src);
                    var dc = fi.CreationTime;
                    var dm = fi.LastWriteTime;
                    var da = fi.LastAccessTime;

                    if (string.IsNullOrEmpty(dst)) dst = src;
                    var fout = keep_name ? src : Path.ChangeExtension(dst, $".{fmt}");

                    var bi = File.ReadAllBytes(src);
                    using (var msi = new MemoryStream(bi))
                    {
                        DateTime dt;
                        var exif = new ExifData(msi);
                        if (exif.ImageType == CompactExifLib.ImageType.Png)
                        {
                            msi.Seek(0, SeekOrigin.Begin);
                            var meta = GetPngMetaInfo(msi, full_field: false);
                            if (meta.ContainsKey("Creation Time") && DateTime.TryParse(Regex.Replace(meta["Creation Time"], @"^(\d{4}):(\d{2})\:(\d{2})(.*?)$", "$1/$2/$3T$4", RegexOptions.IgnoreCase), out dt))
                            {
                                if (!exif.TagExists(ExifTag.DateTime)) exif.SetDateChanged(dt);
                                if (!exif.TagExists(ExifTag.DateTimeDigitized)) exif.SetDateDigitized(dt);
                                if (!exif.TagExists(ExifTag.DateTimeOriginal)) exif.SetDateTaken(dt);
                            }
                            if (!exif.TagExists(ExifTag.XpTitle) && meta.ContainsKey("Title"))
                                exif.SetTagValue(ExifTag.XpTitle, meta["Title"], StrCoding.Utf16Le_Byte);
                            if (!exif.TagExists(ExifTag.XpSubject) && meta.ContainsKey("Subject"))
                                exif.SetTagValue(ExifTag.XpSubject, meta["Subject"], StrCoding.Utf16Le_Byte);
                            //exif.SetTagRawData(ExifTag.XpSubject, ExifTagType.Byte, Encoding.Unicode.GetByteCount(meta["Subject"]), Encoding.Unicode.GetBytes(meta["Subject"]));
                            if (!exif.TagExists(ExifTag.XpAuthor) && meta.ContainsKey("Author"))
                            {
                                exif.SetTagValue(ExifTag.Artist, meta["Author"], StrCoding.Utf8);
                                exif.SetTagValue(ExifTag.XpAuthor, meta["Author"], StrCoding.Utf16Le_Byte);
                                //exif.SetTagRawData(ExifTag.XpAuthor, ExifTagType.Byte, Encoding.Unicode.GetByteCount(meta["Author"]), Encoding.Unicode.GetBytes((meta["Author"]));
                            }
                            if (!exif.TagExists(ExifTag.Copyright) && meta.ContainsKey("Copyright"))
                                exif.SetTagValue(ExifTag.Copyright, meta["Copyright"], StrCoding.Utf8);

                            if (!exif.TagExists(ExifTag.XpComment) && meta.ContainsKey("Description"))
                                exif.SetTagValue(ExifTag.XpComment, meta["Description"], StrCoding.Utf16Le_Byte);
                            if (!exif.TagExists(ExifTag.UserComment) && meta.ContainsKey("Description"))
                                exif.SetTagValue(ExifTag.UserComment, meta["Description"], StrCoding.IdCode_Utf16);

                            if (!exif.TagExists(ExifTag.XpKeywords) && meta.ContainsKey("Comment"))
                                exif.SetTagValue(ExifTag.XpKeywords, meta["Comment"], StrCoding.Utf16Le_Byte);

                            if (!exif.TagExists(ExifTag.FileSource) && meta.ContainsKey("Source"))
                                exif.SetTagValue(ExifTag.FileSource, meta["Source"], StrCoding.Utf8);

                            if (!exif.TagExists(ExifTag.Software) && meta.ContainsKey("Software"))
                                exif.SetTagValue(ExifTag.Software, meta["Software"], StrCoding.Utf8);

                            if (!exif.TagExists(ExifTag.XmpMetadata) && meta.ContainsKey("XML:com.adobe.xmp"))
                            {
                                var value = string.Join("", meta["XML:com.adobe.xmp"].Split(new char[]{ '\0' }).Last().ToArray().SkipWhile(c => c != '<'));
                                exif.SetTagRawData(ExifTag.XmpMetadata, ExifTagType.Byte, Encoding.UTF8.GetByteCount(value), Encoding.UTF8.GetBytes(value));
                            }
                            else if (!exif.TagExists(ExifTag.XmpMetadata) && meta.ContainsKey("Raw profile type xmp"))
                            {
                                var value = string.Join("", meta["Raw profile type xmp"].Split(new char[]{ '\0' }).Last().ToArray().SkipWhile(c => c != '<'));
                                exif.SetTagRawData(ExifTag.XmpMetadata, ExifTagType.Byte, Encoding.UTF8.GetByteCount(value), Encoding.UTF8.GetBytes(value));
                            }
                        }

                        var bo = ConvertImageTo(bi, fmt, quality: quality);
                        using (var msp = new MemoryStream(bo))
                        {
                            var exif_out = new ExifData(msp);
                            exif_out.ReplaceAllTagsBy(exif);
                            using (var mso = new MemoryStream())
                            {
                                msp.Seek(0, SeekOrigin.Begin);
                                exif_out.Save(msp, mso);
                                File.WriteAllBytes(fout, mso.ToArray());
                            }
                        }
                    }

                    var fo = new System.IO.FileInfo(fout);
                    fo.CreationTime = dc;
                    fo.LastWriteTime = dm;
                    fo.LastAccessTime = da;

                    result = fout;
                }
            }
            catch (Exception ex) { ShowMessage(ex.Message); }
            return (result);
        }

        private string ReduceImageFileSize(string src, string dst, string fmt, int quality = 75, bool keep_name = true)
        {
            return (ConvertImageTo(src, dst, fmt, quality, keep_name));
        }

        public override bool Execute<T>(Stream source, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                Result.Reset();
                dynamic status = false;
                if (source is Stream && source.CanRead)
                {
                    if (string.IsNullOrEmpty(_ImageFormat_)) _ImageFormat_ = "jpg";

                    if (_supported_format_.Contains(_ImageFormat_))
                    {
                        var target = ConvertImageTo(source, _ImageFormat_, _ImageQuality_, _KeepName_);
                        status = (dynamic)true;
                    }
                }
                ret = GetReturnValueByStatus(status);
                result = (T)(object)status;

                Result.Set(InputFile, OutputFile, ret, result);
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }

        public override bool Execute<T>(string file, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                Result.Reset();
                dynamic status = false;
                if (File.Exists(file))
                {
                    InputFile = file;
                    var fi = new System.IO.FileInfo(file);
                    var dir = fi.Directory.FullName;
                    var ext = fi.Extension.ToLower();
                    var fmt = ext.TrimStart('.');

                    if (string.IsNullOrEmpty(_ImageFormat_)) _ImageFormat_ = "jpg";

                    if (!_ImageFormat_.Equals(fmt) && _supported_format_.Contains(_ImageFormat_))
                    {
                        var OutputFileExt = $".{_ImageFormat_}";
                        OutputFile = _KeepName_ ? InputFile : Path.Combine(_TargetFolder_.Equals(".") ? dir : _TargetFolder_, Path.ChangeExtension(Path.GetFileName(file), OutputFileExt));
                        if (_OverWrite_ || !File.Exists(OutputFile))
                        {
                            var target = ConvertImageTo(InputFile, OutputFile, _ImageFormat_, _ImageQuality_, _KeepName_);
                            if (File.Exists(OutputFile))
                            {
                                status = _TargetName_ ? (dynamic)OutputFile : (dynamic)true;
                                var fo = new System.IO.FileInfo(OutputFile);
                                fo.CreationTime = fi.CreationTime;
                                fo.LastWriteTime = fi.LastWriteTime;
                                fo.LastAccessTime = fi.LastAccessTime;
                            }
                        }
                    }
                }
                ret = GetReturnValueByStatus(status);
                result = (T)(object)status;

                Result.Set(InputFile, OutputFile, ret, result);
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }

    }

    class Reduce : Applet
    {
        private Convert _instance_ { get; set; } = new Convert();

        private int _ImageQuality_ = 85;
        public int ImageQuality { get { return (_ImageQuality_); } set { _ImageQuality_ = value; } }
        private Color _BGColor_ = Colors.Gray;
        public Color ConvertBGColor { get { return (_BGColor_); } set { _BGColor_ = value; } }

        public override Applet GetApplet()
        {
            return (new Reduce());
        }

        public Reduce()
        {
            var cat = Options[0];
            _instance_ = new Convert();
            Category = _instance_.Category;
            //Options = _instance_.Options;
            //Options[0] = cat;
            var opts = new OptionSet()
            {
                { "q|quality=", "Out Image Quality, default is 85", v => { if (!string.IsNullOrEmpty(v)) int.TryParse(v, out _ImageQuality_); } },
                { "bg|back|bgcolor=", "Set Out Image Background Color, default is Gray", v =>
                    {
                        if (!string.IsNullOrEmpty(v))
                        {
                            try
                            {
                                v = v.Trim('#');
                                if (Regex.IsMatch(v, @"^\#?(([0-9a-f]{2}){2,4}|[0-9a-f]{3})$", RegexOptions.IgnoreCase))
                                    _BGColor_ = (Color)(ColorConverter.ConvertFromString($"#{v}"));
                                else
                                    _BGColor_ = (Color)(ColorConverter.ConvertFromString(v));
                            }
                            catch(Exception ex) { ShowMessage(ex.Message); }
                        }
                    }
                },
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
                if (source is Stream && source.CanRead)
                {
                    _instance_.KeepName = true;
                    _instance_.ImageQuality = _ImageQuality_;
                    _instance_.ConvertBGColor = _BGColor_;
                    ret = _instance_.Execute(source, out result, args);
                }
                Result.Set(InputFile, OutputFile, ret, result);
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }

        public override bool Execute<T>(string file, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                Result.Reset();
                if (File.Exists(file))
                {
                    _instance_.KeepName = true;
                    _instance_.ImageQuality = _ImageQuality_;
                    _instance_.ConvertBGColor = _BGColor_;
                    ret = _instance_.Execute(file, out result, args);
                }
                Result.Set(InputFile, OutputFile, ret, result);
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }

    }
}

