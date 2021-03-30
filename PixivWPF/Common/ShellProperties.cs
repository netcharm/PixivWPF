using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace PixivWPF.Common
{
    public class ShellProperties
    {
        #region Import Methods
        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int SHMultiFileProperties(IDataObject pdtobj, int flags);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ILCreateFromPath(string path);

        [DllImport("shell32.dll", CharSet = CharSet.None)]
        private static extern void ILFree(IntPtr pidl);

        [DllImport("shell32.dll", CharSet = CharSet.None)]
        private static extern int ILGetSize(IntPtr pidl);
        #endregion

        #region Static Methods

        #region Private
        private static MemoryStream CreateShellIDList(StringCollection filenames)
        {
            // first convert all files into pidls list
            int pos = 0;
            byte[][] pidls = new byte[filenames.Count][];
            foreach (var filename in filenames)
            {
                // Get pidl based on name
                IntPtr pidl = ILCreateFromPath(filename);
                int pidlSize = ILGetSize(pidl);
                // Copy over to our managed array
                pidls[pos] = new byte[pidlSize];
                Marshal.Copy(pidl, pidls[pos++], 0, pidlSize);
                ILFree(pidl);
            }

            // Determine where in CIDL we will start pumping PIDLs
            int pidlOffset = 4 * (filenames.Count + 2);
            // Start the CIDL stream
            var memStream = new MemoryStream();
            var sw = new BinaryWriter(memStream);
            // Initialize CIDL witha count of files
            sw.Write(filenames.Count);
            // Calcualte and write relative offsets of every pidl starting with root
            sw.Write(pidlOffset);
            pidlOffset += 4; // root is 4 bytes
            foreach (var pidl in pidls)
            {
                sw.Write(pidlOffset);
                pidlOffset += pidl.Length;
            }

            // Write the root pidl (0) followed by all pidls
            sw.Write(0);
            foreach (var pidl in pidls) sw.Write(pidl);
            // stream now contains the CIDL
            return memStream;
        }
        #endregion

        #region Public 
        public static int Show(IEnumerable<string> Filenames)
        {
            StringCollection Files = new StringCollection();
            Files.AddRange(Filenames.ToArray());
            var data = new DataObject();
            data.SetData("Preferred DropEffect", new MemoryStream(new byte[] { 5, 0, 0, 0 }), true);
            data.SetData("Shell IDList Array", CreateShellIDList(Files), true);
            data.SetData("FileName", Files, true);
            data.SetData("FileNameW", Files, true);
            data.SetFileDropList(Files);
            return (SHMultiFileProperties(data, 0));
        }

        public static int Show(params string[] Filenames)
        {
            return Show(Filenames as IEnumerable<string>);
        }
        #endregion
        //private const int SW_SHOW = 5;
        //private const uint SEE_MASK_INVOKEIDLIST = 12;

        //[DllImport("shell32.dll")]
        //static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        //[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        //public struct SHELLEXECUTEINFO
        //{
        //    public int cbSize;
        //    public uint fMask;
        //    public IntPtr hwnd;
        //    public string lpVerb;
        //    public string lpFile;
        //    public string lpParameters;
        //    public string lpDirectory;
        //    public int nShow;
        //    public int hInstApp;
        //    public int lpIDList;
        //    public string lpClass;
        //    public int hkeyClass;
        //    public uint dwHotKey;
        //    public int hIcon;
        //    public int hProcess;
        //}

        //public static void ShowFileProperties(string Filename)
        //{
        //    SHELLEXECUTEINFO info = new SHELLEXECUTEINFO();
        //    info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(info);
        //    info.lpVerb = "properties";
        //    info.lpFile = Filename;
        //    info.nShow = SW_SHOW;
        //    info.fMask = SEE_MASK_INVOKEIDLIST;
        //    ShellExecuteEx(ref info);
        //}

        public static bool ShowFileProperties(params string[] FileNames)
        {
            bool result = false;
            try
            {
                var pdtobj = new DataObject();
                var flist = new StringCollection();
                flist.AddRange(FileNames);
                pdtobj.SetFileDropList(flist);
                if (SHMultiFileProperties(pdtobj, 0) == 0 /*S_OK*/) result = true;
            }
            catch (Exception ex) { ex.ERROR("ShowFileProperties"); }
            return (result);
        }
        #endregion
    }
}
