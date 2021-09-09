﻿//// /ew option for compiling to windows execution file
//// /e  option for compiling to console execution file
//css_args /co:/win32icon:./PixivWPF/pixiv-logo.ico
//css_co /win32icon:./PixivWPF/pixiv-logo.ico

//css_reference WindowsBase.dll
//css_reference PresentationCore.dll
//css_reference PresentationFramework.dll
//css_reference Microsoft.WindowsAPICodePack.dll
//css_reference Microsoft.WindowsAPICodePack.Shell.dll
//css_reference System.Windows.Forms.dll
//css_reference Newtonsoft.Json.dll

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[assembly: AssemblyTitle("PixivWPF Search Bridge Utility")]
//[assembly: AssemblyDescription("PixivWPF Search Bridge Utility")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("NetCharm")]
[assembly: AssemblyProduct("PixivWPF Search Bridge Utility")]
[assembly: AssemblyCopyright("Copyright NetCharm © 2020")]
[assembly: AssemblyTrademark("NetCharm")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

namespace netcharm
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
            Console.WriteLine(Files[0]);
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ShowFileProperties: {ex.Message}"); }
            return (result);
        }
        #endregion
    }

    class PixivWPFSearch
    {
        private static string AppPath = Path.GetDirectoryName(Application.ResourceAssembly.CodeBase.ToString()).Replace("file:\\", "");
        public static void Main(string[] args)
        {
            try
            {
                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                //ShowTaskDialog($"{AppPath}", "Ready?");
                //return;
                if (args.Length < 1) return;
                //args = Environment.GetCommandLineArgs();
                //Console.WriteLine(string.Join(", ", Environment.GetCommandLineArgs()));
                //var args_alt = Environment.CommandLine.Split();
                var args_alt = Environment.GetCommandLineArgs();
                if (args[0].Equals("upgrade", StringComparison.CurrentCultureIgnoreCase))
                {
                    var files = args.Skip(1).ToArray();
                    if (files == null || files.Length <= 0)
                    {
                        var IsExe = args_alt.Length >= 2 && args_alt[1].Equals(args[0], StringComparison.CurrentCultureIgnoreCase);
                        var cfgfile = Path.Combine(IsExe ? AppPath : "", "config.json");
                        //Console.WriteLine(cfgfile);
                        if (File.Exists(cfgfile))
                        {
                            var json = File.ReadAllText(cfgfile);
                            JToken token = JToken.Parse(json);
                            JToken upgradelist = token.SelectToken("$..UpgradeFiles", false);
                            if (upgradelist != null)
                            {
                                try
                                {
                                    files = upgradelist.ToObject<List<string>>().ToArray();
                                    //Console.WriteLine(string.Join(", ", files));
                                }
                                catch { }
                            }
                        }
                    }
                    UpgradeFiles(files);
                }
                else if (args[0].Equals("properties", StringComparison.CurrentCultureIgnoreCase))
                    ShowFileProperties(args.Skip(1).ToArray());
                else
                    SendBridgeCmd(args);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "ERROR!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void SendBridgeCmd(string[] datas)
        {
            var sendData = string.Join(Environment.NewLine, datas);
            if (string.IsNullOrEmpty(sendData.Trim())) return;

            var pipes = System.IO.Directory.GetFiles("\\\\.\\pipe\\", "PixivWPF*");
#if DEBUG
                if (pipes.Length > 0)
                {
                    Console.WriteLine($"Found {pipes.Length} PixivWPF-Search Bridge(s):");
                    foreach (var pipe in pipes)
                    {
                        Console.WriteLine($"  {pipe}");
                    }
                }
                else return;
#endif

            foreach (var pipe in pipes)
            {
                try
                {
                    var pipeName = pipe.Substring(9);
                    using (var pipeClient = new NamedPipeClientStream(".", pipeName,
                        PipeDirection.Out, PipeOptions.Asynchronous,
                        System.Security.Principal.TokenImpersonationLevel.Impersonation))
                    {
                        pipeClient.Connect(1000);
                        using (StreamWriter sw = new StreamWriter(pipeClient))
                        {
#if DEBUG
                                Console.WriteLine($"Sending [{sendData}] to {pipeName}");
#endif
                            sw.WriteLine(sendData);
                            sw.Flush();
                        }
                    }
                }
#if DEBUG
                    catch(Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "ERROR!", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
#else
                catch (Exception) { }
#endif
            }
        }

        public static bool ShowTaskDialog(string content, string title)
        {
            var dialog = new TaskDialog()
            {
                Cancelable = true,
                StandardButtons = TaskDialogStandardButtons.Cancel | TaskDialogStandardButtons.Ok,
                Icon = TaskDialogStandardIcon.Warning,
                FooterIcon = TaskDialogStandardIcon.Warning,
                ExpansionMode = TaskDialogExpandedDetailsLocation.ExpandFooter,
                DetailsExpanded = false,
                DetailsExpandedText = content,
                Text = title,
                FooterText = title
            };
            var ret = dialog.Show();
            var result = ret == TaskDialogResult.Ok ? true : false;
            return (result);
        }

        private static bool IsFileLocked(string file)
        {
            try
            {
                using (FileStream stream = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (Exception)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }

            //file is not locked
            return false;
        }

        private static bool IsFileLocked(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (Exception)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }

            //file is not locked
            return false;
        }

        private static void UpgradeFiles(string[] files)
        {
            if (files.Length <= 0) return;
            var wait_count = 0;
            do
            {
                if (System.IO.Directory.GetFiles("\\\\.\\pipe\\", "PixivWPF*").Count() <= 0) break;
                System.Threading.Thread.Sleep(1000);
                wait_count++;
            } while (wait_count < 60);
            //System.Threading.Thread.Sleep(2000);
            System.Threading.Tasks.Task.Delay(5000).GetAwaiter().GetResult();

            List<string> f_upgraded = new List<string>();
            List<string> f_skiped = new List<string>();
            f_upgraded.Add($"Upgrade file ...");
            f_skiped.Add($"Skiped file ...");
            foreach (var f_remote in files)
            {
                var fn = Path.GetFileName(f_remote);
                var f_local = Path.Combine(AppPath, fn);
                var fi_local = new FileInfo(f_local);
                if (!File.Exists(f_remote)) continue;
                var fi_remote = new FileInfo(f_remote);
                if (!File.Exists(f_local) || fi_local.LastWriteTime < fi_remote.LastWriteTime)
                {
                    //f_upgraded.Add($"Upgrade file ...");
                    //f_upgraded.Add($"  From : {f_remote}");
                    //f_upgraded.Add($"  To   : {f_local}");
                    f_upgraded.Add($"  {fn}");

                    wait_count = 0;
                    while (IsFileLocked(fi_local) && wait_count < 10)
                    {
                        System.Threading.Thread.Sleep(1000);
                        System.Threading.Tasks.Task.Delay(1000).GetAwaiter().GetResult();
                        wait_count++;
                    };
                    File.Copy(f_remote, f_local, true);
                }
                else
                {
                    //f_skiped.Add($"Skiped file ...");
                    //f_skiped.Add($"  From : {f_remote}");
                    //f_skiped.Add($"  To   : {f_local}");
                    f_skiped.Add($"  {fn}");
                }
            }
            if (f_upgraded.Count > 0)
            {
                f_upgraded.Insert(0, "Upgraded Finished!");
                f_upgraded.Add("Start Application?");
                var ret = MessageBox.Show(string.Join(Environment.NewLine, f_upgraded), "Congratulation!", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (ret == MessageBoxResult.OK || ret == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(Path.Combine(AppPath, "PixivWPF.exe"));
                }
            }
            else
            {
                var ret = MessageBox.Show("Start Application?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (ret == MessageBoxResult.OK || ret == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(Path.Combine(AppPath, "PixivWPF.exe"));
                }
            }
        }

        #region Public 
        public static int ShowPropertiesDialog(IEnumerable<string> Filenames)
        {
            return (ShellProperties.Show(Filenames));
        }

        public static int ShowPropertiesDialog(params string[] Filenames)
        {
            return (ShowPropertiesDialog(Filenames as IEnumerable<string>));
        }
        #endregion

        public static void ShowFileProperties(IEnumerable<string> Files)
        {
            Console.WriteLine(ShowPropertiesDialog(Files));
        }
    }
}