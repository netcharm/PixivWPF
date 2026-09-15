using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PixivWPF.Common
{
    internal class Everything
    {
        private Everything32 everything32;
        private Everything64 everything64;

        private ConcurrentDictionary<string, List<string>> _files_;

        public bool IsAvailable
        {
            get
            {
                if (Environment.Is64BitProcess)
                    return (everything64 != null && everything64.Loaded);
                else
                    return (everything32 != null && everything32.Loaded);
            }
        }

        public uint LastError => Environment.Is64BitProcess ? (everything64?.LastError ?? 0) : (everything32?.LastError ?? 0);

        public Everything()
        {
            if (Environment.Is64BitProcess)
            {
                //
                if (System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Everything64.dll")))
                    everything64 = new Everything64();
            }
            else
            {
                //
                if (System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Everything32.dll")))
                    everything32 = new Everything32();
            }
        }

        public IEnumerable<string> GetFiles(string path, string pattern = "*.*", bool nested = false)
        {
            var result = new List<string>();
            //
            _files_ ??= new();
            if (_files_.IsEmpty || !_files_.ContainsKey(path) || _files_[path] == null || !_files_[path].Any())
            {
                UpdateFiles(path, pattern, nested);
                ////var files = Environment.Is64BitProcess ? everything64?.GetFiles(path, pattern) : everything32?.GetFiles(path, pattern);
                //var files = Environment.Is64BitProcess ? everything64?.GetFiles(path, "*.*", nested) : everything32?.GetFiles(path, "*.*", nested);
                //if (files.Any())
                //{
                //    _files_[path] = [];
                //    if (nested) 
                //        _files_[path].AddRange(files);
                //    else
                //        _files_[path].AddRange(files.Where(f => string.IsNullOrEmpty(System.IO.Path.GetDirectoryName(f))));
                //    $"Get {result.Count} Files".DEBUG("EverythingGetFiles");
                //}
            }
            if (_files_.ContainsKey(path))
            {
                //result = nested ? [.. _files_[path].Where(f => f.StartsWith(path, StringComparison.CurrentCultureIgnoreCase))] : [.. _files_[path]];
                result = [.. _files_[path].Where(f => Regex.IsMatch(f, $"\\{pattern}", RegexOptions.IgnoreCase)).Select(f => System.IO.Path.Combine(path, f))];
            }
            return (result);
        }

        public IEnumerable<string> EnumerateFiles(string path, string pattern = "*.*", bool nested = false)
        {
            var files = Environment.Is64BitProcess ? everything64?.EnumerateFiles(path, "*.*", nested) : everything32?.EnumerateFiles(path, "*.*", nested);
            var result = files.Where(f => Regex.IsMatch(f, $"\\{pattern}", RegexOptions.IgnoreCase)).Select(f => System.IO.Path.Combine(path, f));
            foreach (var f in result) yield return (f);
        }
        
        public bool UpdateFiles(string path, string pattern = "*.*", bool nested = false)
        {
            var result = false;
            _files_ ??= new();
            try
            {
                if (_files_.ContainsKey(path)) _files_[path] = [];
                else _files_.TryAdd(path, []);

                var files = Environment.Is64BitProcess ? everything64?.GetFiles(path, "*.*", nested) : everything32?.GetFiles(path, "*.*", nested);
                if (files.Any())
                {
                    if (nested)
                        _files_[path].AddRange(files.Distinct());
                    else
                        _files_[path].AddRange(files.Where(f => string.IsNullOrEmpty(System.IO.Path.GetDirectoryName(f))).Distinct());
                    result = true;
                    $"Update {files.Count()} Files".DEBUG("EverythingUpdateFiles");
                }
            }
            catch (Exception ex) { ex.ERROR("EverythingUpdateFiles"); }
            return (result);
        }

        private CancellationTokenSource _cancel_update_ = new();
        private SemaphoreSlim _update_files_ = new(1, 1);
        public async void UpdateFilesAsync(string path, string pattern = "*.*", bool nested = false)
        {
            _cancel_update_ ??= new();
            _cancel_update_.Cancel();
            await Task.Delay(50);
            _cancel_update_ = new();
            try
            {
                await Task.Run(async () =>
                {
                    if (await _update_files_?.WaitAsync(TimeSpan.FromSeconds(5), _cancel_update_.Token))
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5), _cancel_update_.Token);
                            _cancel_update_.Token.ThrowIfCancellationRequested();
                            UpdateFiles(path, pattern, nested);
                        }
                        catch (OperationCanceledException)
                        {
                            // Handle cancellation if needed
                            "Update Canceled".DEBUG("EverythingUpdateFiles");
                        }
                        finally
                        {
                            if (_update_files_?.CurrentCount == 0) _update_files_?.Release();
                        }
                    }
                }, _cancel_update_.Token);
            }
            catch (Exception ex) { ex.ERROR("EverythingUpdateFilesAsync"); }
        }

        public bool UpdateFile(string file_new, string file_old = "", WatcherChangeTypes change = WatcherChangeTypes.All)
        {
            var result = false;
            _files_ ??= new();
            try
            {
                if (string.IsNullOrEmpty(file_new.Trim()) || change == WatcherChangeTypes.All || change == WatcherChangeTypes.Changed) return (false);

                if (!System.IO.Path.IsPathRooted(file_new)) file_new = System.IO.Path.GetFullPath(file_new);
                var fn_path = System.IO.Path.GetDirectoryName(file_new);
                var fn_name = System.IO.Path.GetFileName(file_new);
                foreach (var folder in _files_.Keys.OrderBy(k => k.Length))
                {
                    if (file_new.StartsWith(folder, StringComparison.CurrentCultureIgnoreCase))
                    {
                        fn_path = folder;
                        fn_name = file_new.Substring(folder.Length).TrimStart('\\');
                        break;
                    }
                }
                if (!_files_.ContainsKey(fn_path)) _files_[fn_path] = [];
                if (change == WatcherChangeTypes.Deleted)
                {
                    _files_[fn_path].RemoveAll(f => string.Equals(f, fn_name, StringComparison.CurrentCultureIgnoreCase));
                    result = true;
                    $"Delete File: \"{file_new}\"".DEBUG("EverythingUpdateFile");
                }
                else if (change == WatcherChangeTypes.Renamed && !string.IsNullOrEmpty(file_old.Trim()))
                {
                    // Handle rename logic if needed
                    if (!System.IO.Path.IsPathRooted(file_old)) file_old = System.IO.Path.GetFullPath(file_old);
                    var fo_path = System.IO.Path.GetDirectoryName(file_old);
                    var fo_name = System.IO.Path.GetFileName(file_old);
                    foreach (var folder in _files_.Keys.OrderBy(k => k.Length))
                    {
                        if (file_new.StartsWith(folder, StringComparison.CurrentCultureIgnoreCase))
                        {
                            fo_path = folder;
                            fo_name = file_new.Substring(folder.Length).TrimStart('\\');
                            break;
                        }
                    }
                    _files_[fo_path].RemoveAll(f => string.Equals(f, fo_name, StringComparison.CurrentCultureIgnoreCase));
                    _files_[fn_path].Add(fn_name);
                    result = true;
                    $"Rename File: from \"{file_old}\" to \"{file_new}\"".DEBUG("EverythingUpdateFile");
                }
                else if (change == WatcherChangeTypes.Created)
                {
                    _files_[fn_path].Add(fn_name);
                    result = true;
                    $"Change File: \"{file_new}\"".DEBUG("EverythingUpdateFile");
                }
                else
                {
                    //if (!_files_[path].Contains(filename)) _files_[path].Add(filename);
                    //$"Add/Update File: {file}".DEBUG("EverythingUpdateFile");
                }
            }
            catch (Exception ex) { ex.ERROR("EverythingUpdateFile"); }
            return (result);
        }

        private SemaphoreSlim _update_file_ = new(1, 1);
        public async void UpdateFileAsync(string file_new, string file_old = "", WatcherChangeTypes change = WatcherChangeTypes.All)
        {
            try
            {
                _update_file_ ??= new(1, 1);
                await Task.Run(async () =>
                {
                    if (await _update_file_?.WaitAsync(TimeSpan.FromSeconds(5), _cancel_update_?.Token ?? CancellationToken.None))
                    {
                        try
                        {
                            UpdateFile(file_new, file_old, change);
                        }
                        catch (OperationCanceledException)
                        {
                            // Handle cancellation if needed
                            if (string.IsNullOrEmpty(file_old.Trim()))
                                $"Update \"{file_new}\" Canceled".DEBUG("EverythingUpdateFile");
                            else
                                $"Update from \"{file_old}\" to \"{file_new}\" Canceled".DEBUG("EverythingUpdateFile");
                        }
                        finally
                        {
                            if (_update_file_?.CurrentCount == 0) _update_file_?.Release();
                        }
                    }
                }, _cancel_update_?.Token ?? CancellationToken.None);
            }
            catch (Exception ex) { ex.ERROR("EverythingUpdateFilesAsync"); }
        }

        public bool FileExists(string path, string pattern = "*.*", bool nested = false)
        {
            return (Environment.Is64BitProcess ? everything64?.FileExists(path, pattern, nested) : everything32?.FileExists(path, pattern, nested)) ?? false;
        }
    }

    internal class Everything32
    {
        #region Load DLL 
        const int EVERYTHING_OK = 0;
        const int EVERYTHING_ERROR_MEMORY = 1;
        const int EVERYTHING_ERROR_IPC = 2;
        const int EVERYTHING_ERROR_REGISTERCLASSEX = 3;
        const int EVERYTHING_ERROR_CREATEWINDOW = 4;
        const int EVERYTHING_ERROR_CREATETHREAD = 5;
        const int EVERYTHING_ERROR_INVALIDINDEX = 6;
        const int EVERYTHING_ERROR_INVALIDCALL = 7;

        const int EVERYTHING_REQUEST_FILE_NAME = 0x00000001;
        const int EVERYTHING_REQUEST_PATH = 0x00000002;
        const int EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004;
        const int EVERYTHING_REQUEST_EXTENSION = 0x00000008;
        const int EVERYTHING_REQUEST_SIZE = 0x00000010;
        const int EVERYTHING_REQUEST_DATE_CREATED = 0x00000020;
        const int EVERYTHING_REQUEST_DATE_MODIFIED = 0x00000040;
        const int EVERYTHING_REQUEST_DATE_ACCESSED = 0x00000080;
        const int EVERYTHING_REQUEST_ATTRIBUTES = 0x00000100;
        const int EVERYTHING_REQUEST_FILE_LIST_FILE_NAME = 0x00000200;
        const int EVERYTHING_REQUEST_RUN_COUNT = 0x00000400;
        const int EVERYTHING_REQUEST_DATE_RUN = 0x00000800;
        const int EVERYTHING_REQUEST_DATE_RECENTLY_CHANGED = 0x00001000;
        const int EVERYTHING_REQUEST_HIGHLIGHTED_FILE_NAME = 0x00002000;
        const int EVERYTHING_REQUEST_HIGHLIGHTED_PATH = 0x00004000;
        const int EVERYTHING_REQUEST_HIGHLIGHTED_FULL_PATH_AND_FILE_NAME = 0x00008000;

        const int EVERYTHING_SORT_NAME_ASCENDING = 1;
        const int EVERYTHING_SORT_NAME_DESCENDING = 2;
        const int EVERYTHING_SORT_PATH_ASCENDING = 3;
        const int EVERYTHING_SORT_PATH_DESCENDING = 4;
        const int EVERYTHING_SORT_SIZE_ASCENDING = 5;
        const int EVERYTHING_SORT_SIZE_DESCENDING = 6;
        const int EVERYTHING_SORT_EXTENSION_ASCENDING = 7;
        const int EVERYTHING_SORT_EXTENSION_DESCENDING = 8;
        const int EVERYTHING_SORT_TYPE_NAME_ASCENDING = 9;
        const int EVERYTHING_SORT_TYPE_NAME_DESCENDING = 10;
        const int EVERYTHING_SORT_DATE_CREATED_ASCENDING = 11;
        const int EVERYTHING_SORT_DATE_CREATED_DESCENDING = 12;
        const int EVERYTHING_SORT_DATE_MODIFIED_ASCENDING = 13;
        const int EVERYTHING_SORT_DATE_MODIFIED_DESCENDING = 14;
        const int EVERYTHING_SORT_ATTRIBUTES_ASCENDING = 15;
        const int EVERYTHING_SORT_ATTRIBUTES_DESCENDING = 16;
        const int EVERYTHING_SORT_FILE_LIST_FILENAME_ASCENDING = 17;
        const int EVERYTHING_SORT_FILE_LIST_FILENAME_DESCENDING = 18;
        const int EVERYTHING_SORT_RUN_COUNT_ASCENDING = 19;
        const int EVERYTHING_SORT_RUN_COUNT_DESCENDING = 20;
        const int EVERYTHING_SORT_DATE_RECENTLY_CHANGED_ASCENDING = 21;
        const int EVERYTHING_SORT_DATE_RECENTLY_CHANGED_DESCENDING = 22;
        const int EVERYTHING_SORT_DATE_ACCESSED_ASCENDING = 23;
        const int EVERYTHING_SORT_DATE_ACCESSED_DESCENDING= 24;
        const int EVERYTHING_SORT_DATE_RUN_ASCENDING = 25;
        const int EVERYTHING_SORT_DATE_RUN_DESCENDING = 26;

        const int EVERYTHING_TARGET_MACHINE_X86 = 1;
        const int EVERYTHING_TARGET_MACHINE_X64 = 2;
        const int EVERYTHING_TARGET_MACHINE_ARM = 3;

        [DllImport("Everything32.dll", CharSet = CharSet.Unicode)]
        public static extern UInt32 Everything_SetSearchW(string lpSearchString);
        [DllImport("Everything32.dll")]
        public static extern void Everything_SetMatchPath(bool bEnable);
        [DllImport("Everything32.dll")]
        public static extern void Everything_SetMatchCase(bool bEnable);
        [DllImport("Everything32.dll")]
        public static extern void Everything_SetMatchWholeWord(bool bEnable);
        [DllImport("Everything32.dll")]
        public static extern void Everything_SetRegex(bool bEnable);
        [DllImport("Everything32.dll")]
        public static extern void Everything_SetMax(UInt32 dwMax);
        [DllImport("Everything32.dll")]
        public static extern void Everything_SetOffset(UInt32 dwOffset);

        [DllImport("Everything32.dll")]
        public static extern bool Everything_GetMatchPath();
        [DllImport("Everything32.dll")]
        public static extern bool Everything_GetMatchCase();
        [DllImport("Everything32.dll")]
        public static extern bool Everything_GetMatchWholeWord();
        [DllImport("Everything32.dll")]
        public static extern bool Everything_GetRegex();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetMax();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetOffset();
        [DllImport("Everything32.dll")]
        public static extern IntPtr Everything_GetSearchW();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetLastError();

        [DllImport("Everything32.dll")]
        public static extern bool Everything_QueryW(bool bWait);

        [DllImport("Everything32.dll")]
        public static extern void Everything_SortResultsByPath();

        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetNumFileResults();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetNumFolderResults();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetNumResults();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetTotFileResults();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetTotFolderResults();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetTotResults();
        [DllImport("Everything32.dll")]
        public static extern bool Everything_IsVolumeResult(UInt32 nIndex);
        [DllImport("Everything32.dll")]
        public static extern bool Everything_IsFolderResult(UInt32 nIndex);
        [DllImport("Everything32.dll")]
        public static extern bool Everything_IsFileResult(UInt32 nIndex);
        [DllImport("Everything32.dll", CharSet = CharSet.Unicode)]
        public static extern void Everything_GetResultFullPathName(UInt32 nIndex, StringBuilder lpString, UInt32 nMaxCount);
        [DllImport("Everything32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultPath(UInt32 nIndex);
        [DllImport("Everything32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultFileName(UInt32 nIndex);

        [DllImport("Everything32.dll")]
        public static extern void Everything_Reset();
        [DllImport("Everything32.dll")]
        public static extern void Everything_CleanUp();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetMajorVersion();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetMinorVersion();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetRevision();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetBuildNumber();
        [DllImport("Everything32.dll")]
        public static extern bool Everything_Exit();
        [DllImport("Everything32.dll")]
        public static extern bool Everything_IsDBLoaded();
        [DllImport("Everything32.dll")]
        public static extern bool Everything_IsAdmin();
        [DllImport("Everything32.dll")]
        public static extern bool Everything_IsAppData();
        [DllImport("Everything32.dll")]
        public static extern bool Everything_RebuildDB();
        [DllImport("Everything32.dll")]
        public static extern bool Everything_UpdateAllFolderIndexes();
        [DllImport("Everything32.dll")]
        public static extern bool Everything_SaveDB();
        [DllImport("Everything32.dll")]
        public static extern bool Everything_SaveRunHistory();
        [DllImport("Everything32.dll")]
        public static extern bool Everything_DeleteRunHistory();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetTargetMachine();

        // Everything 1.4
        [DllImport("Everything32.dll")]
        public static extern void Everything_SetSort(UInt32 dwSortType);
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetSort();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetResultListSort();
        [DllImport("Everything32.dll")]
        public static extern void Everything_SetRequestFlags(UInt32 dwRequestFlags);
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetRequestFlags();
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetResultListRequestFlags();
        [DllImport("Everything32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultExtension(UInt32 nIndex);
        [DllImport("Everything32.dll")]
        public static extern bool Everything_GetResultSize(UInt32 nIndex, out long lpFileSize);
        [DllImport("Everything32.dll")]
        public static extern bool Everything_GetResultDateCreated(UInt32 nIndex, out long lpFileTime);
        [DllImport("Everything32.dll")]
        public static extern bool Everything_GetResultDateModified(UInt32 nIndex, out long lpFileTime);
        [DllImport("Everything32.dll")]
        public static extern bool Everything_GetResultDateAccessed(UInt32 nIndex, out long lpFileTime);
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetResultAttributes(UInt32 nIndex);
        [DllImport("Everything32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultFileListFileName(UInt32 nIndex);
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetResultRunCount(UInt32 nIndex);
        [DllImport("Everything32.dll")]
        public static extern bool Everything_GetResultDateRun(UInt32 nIndex, out long lpFileTime);
        [DllImport("Everything32.dll")]
        public static extern bool Everything_GetResultDateRecentlyChanged(UInt32 nIndex, out long lpFileTime);
        [DllImport("Everything32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultHighlightedFileName(UInt32 nIndex);
        [DllImport("Everything32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultHighlightedPath(UInt32 nIndex);
        [DllImport("Everything32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultHighlightedFullPathAndFileName(UInt32 nIndex);
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_GetRunCountFromFileName(string lpFileName);
        [DllImport("Everything32.dll")]
        public static extern bool Everything_SetRunCountFromFileName(string lpFileName, UInt32 dwRunCount);
        [DllImport("Everything32.dll")]
        public static extern UInt32 Everything_IncRunCountFromFileName(string lpFileName);
        #endregion

        const int MAX_PATH = 260;

        public bool Loaded => Everything_IsDBLoaded();

        public uint LastError => Everything_GetLastError();

        public IEnumerable<string> GetFiles(string path, string pattern = "*.*", bool nested = false)
        {
            var result = new List<string>();
            try
            {
                var query = nested ? $"file:{path.TrimEnd('\\')}\\ {pattern}" : $"file:{System.IO.Path.Combine(path, pattern)} parent:{path}";
                
                //Everything_Reset();
                Everything_SetSearchW(query);
                Everything_SetMatchPath(true);
                Everything_SetRequestFlags(EVERYTHING_REQUEST_FILE_NAME | EVERYTHING_REQUEST_PATH | EVERYTHING_REQUEST_DATE_MODIFIED | EVERYTHING_REQUEST_SIZE);
                Everything_SetSort(EVERYTHING_SORT_NAME_ASCENDING);
                Everything_QueryW(true);

                var count = Everything_GetNumResults();
                for (uint i = 0; i < count; i++)
                {
                    var fullpath = new StringBuilder(MAX_PATH);
                    Everything_GetResultFullPathName(i, fullpath, MAX_PATH);
                    result.Add(fullpath.ToString().Replace(path, "").TrimStart('\\'));
                    //var f = Marshal.PtrToStringUni(Everything_GetResultFileName(i));
                    //result.Add(Marshal.PtrToStringUni(Everything_GetResultFileName(i)));
                }
            }
            catch (Exception ex) { ex.ERROR("EverythingGetFiles"); }
            return (result);
        }

        public IEnumerable<string> EnumerateFiles(string path, string pattern = "*.*", bool nested = false)
        {
            var result = new List<string>();
            try
            {
                var query = nested ? $"file:{path.TrimEnd('\\')}\\ {pattern}" : $"file:{System.IO.Path.Combine(path, pattern)} parent:{path}";

                //Everything_Reset();
                Everything_SetSearchW(query);
                Everything_SetMatchPath(true);
                Everything_SetRequestFlags(EVERYTHING_REQUEST_FILE_NAME | EVERYTHING_REQUEST_PATH | EVERYTHING_REQUEST_DATE_MODIFIED | EVERYTHING_REQUEST_SIZE);
                Everything_SetSort(EVERYTHING_SORT_NAME_ASCENDING);
                Everything_QueryW(true);

            }
            catch (Exception ex) { ex.ERROR("EverythingGetFiles"); }

            var count = Everything_GetNumResults();
            for (uint i = 0; i < count; i++)
            {
                var fullpath = new StringBuilder(MAX_PATH);
                Everything_GetResultFullPathName(i, fullpath, MAX_PATH);
                yield return (fullpath.ToString().Replace(path, "").TrimStart('\\'));
            }
        }

        public bool FileExists(string path, string pattern = "*.*", bool nested = false)
        {
            var result = false;
            try
            {
                var files = GetFiles(path, pattern, nested);
                result = files.Any();
            }
            catch (Exception ex)
            {
                // Handle the exception as needed
                //Console.WriteLine($"Error checking file existence: {ex.Message}");
                ex.ERROR("EverythingFileExists");
            }
            return (result);
        }
    }

    internal class Everything64
    {
        #region Load DLL
        const int EVERYTHING_OK = 0;
        const int EVERYTHING_ERROR_MEMORY = 1;
        const int EVERYTHING_ERROR_IPC = 2;
        const int EVERYTHING_ERROR_REGISTERCLASSEX = 3;
        const int EVERYTHING_ERROR_CREATEWINDOW = 4;
        const int EVERYTHING_ERROR_CREATETHREAD = 5;
        const int EVERYTHING_ERROR_INVALIDINDEX = 6;
        const int EVERYTHING_ERROR_INVALIDCALL = 7;

        const int EVERYTHING_REQUEST_FILE_NAME = 0x00000001;
        const int EVERYTHING_REQUEST_PATH = 0x00000002;
        const int EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004;
        const int EVERYTHING_REQUEST_EXTENSION = 0x00000008;
        const int EVERYTHING_REQUEST_SIZE = 0x00000010;
        const int EVERYTHING_REQUEST_DATE_CREATED = 0x00000020;
        const int EVERYTHING_REQUEST_DATE_MODIFIED = 0x00000040;
        const int EVERYTHING_REQUEST_DATE_ACCESSED = 0x00000080;
        const int EVERYTHING_REQUEST_ATTRIBUTES = 0x00000100;
        const int EVERYTHING_REQUEST_FILE_LIST_FILE_NAME = 0x00000200;
        const int EVERYTHING_REQUEST_RUN_COUNT = 0x00000400;
        const int EVERYTHING_REQUEST_DATE_RUN = 0x00000800;
        const int EVERYTHING_REQUEST_DATE_RECENTLY_CHANGED = 0x00001000;
        const int EVERYTHING_REQUEST_HIGHLIGHTED_FILE_NAME = 0x00002000;
        const int EVERYTHING_REQUEST_HIGHLIGHTED_PATH = 0x00004000;
        const int EVERYTHING_REQUEST_HIGHLIGHTED_FULL_PATH_AND_FILE_NAME = 0x00008000;

        const int EVERYTHING_SORT_NAME_ASCENDING = 1;
        const int EVERYTHING_SORT_NAME_DESCENDING = 2;
        const int EVERYTHING_SORT_PATH_ASCENDING = 3;
        const int EVERYTHING_SORT_PATH_DESCENDING = 4;
        const int EVERYTHING_SORT_SIZE_ASCENDING = 5;
        const int EVERYTHING_SORT_SIZE_DESCENDING = 6;
        const int EVERYTHING_SORT_EXTENSION_ASCENDING = 7;
        const int EVERYTHING_SORT_EXTENSION_DESCENDING = 8;
        const int EVERYTHING_SORT_TYPE_NAME_ASCENDING = 9;
        const int EVERYTHING_SORT_TYPE_NAME_DESCENDING = 10;
        const int EVERYTHING_SORT_DATE_CREATED_ASCENDING = 11;
        const int EVERYTHING_SORT_DATE_CREATED_DESCENDING = 12;
        const int EVERYTHING_SORT_DATE_MODIFIED_ASCENDING = 13;
        const int EVERYTHING_SORT_DATE_MODIFIED_DESCENDING = 14;
        const int EVERYTHING_SORT_ATTRIBUTES_ASCENDING = 15;
        const int EVERYTHING_SORT_ATTRIBUTES_DESCENDING = 16;
        const int EVERYTHING_SORT_FILE_LIST_FILENAME_ASCENDING = 17;
        const int EVERYTHING_SORT_FILE_LIST_FILENAME_DESCENDING = 18;
        const int EVERYTHING_SORT_RUN_COUNT_ASCENDING = 19;
        const int EVERYTHING_SORT_RUN_COUNT_DESCENDING = 20;
        const int EVERYTHING_SORT_DATE_RECENTLY_CHANGED_ASCENDING = 21;
        const int EVERYTHING_SORT_DATE_RECENTLY_CHANGED_DESCENDING = 22;
        const int EVERYTHING_SORT_DATE_ACCESSED_ASCENDING = 23;
        const int EVERYTHING_SORT_DATE_ACCESSED_DESCENDING = 24;
        const int EVERYTHING_SORT_DATE_RUN_ASCENDING = 25;
        const int EVERYTHING_SORT_DATE_RUN_DESCENDING = 26;

        const int EVERYTHING_TARGET_MACHINE_X86 = 1;
        const int EVERYTHING_TARGET_MACHINE_X64 = 2;
        const int EVERYTHING_TARGET_MACHINE_ARM = 3;

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern UInt32 Everything_SetSearchW(string lpSearchString);
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetMatchPath(bool bEnable);
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetMatchCase(bool bEnable);
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetMatchWholeWord(bool bEnable);
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetRegex(bool bEnable);
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetMax(UInt32 dwMax);
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetOffset(UInt32 dwOffset);

        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetMatchPath();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetMatchCase();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetMatchWholeWord();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetRegex();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetMax();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetOffset();
        [DllImport("Everything64.dll")]
        public static extern IntPtr Everything_GetSearchW();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetLastError();

        [DllImport("Everything64.dll")]
        public static extern bool Everything_QueryW(bool bWait);

        [DllImport("Everything64.dll")]
        public static extern void Everything_SortResultsByPath();

        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetNumFileResults();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetNumFolderResults();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetNumResults();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetTotFileResults();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetTotFolderResults();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetTotResults();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_IsVolumeResult(UInt32 nIndex);
        [DllImport("Everything64.dll")]
        public static extern bool Everything_IsFolderResult(UInt32 nIndex);
        [DllImport("Everything64.dll")]
        public static extern bool Everything_IsFileResult(UInt32 nIndex);
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern void Everything_GetResultFullPathName(UInt32 nIndex, StringBuilder lpString, UInt32 nMaxCount);

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultFileName(UInt32 nIndex);

        [DllImport("Everything64.dll")]
        public static extern void Everything_Reset();
        [DllImport("Everything64.dll")]
        public static extern void Everything_CleanUp();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetMajorVersion();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetMinorVersion();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetRevision();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetBuildNumber();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_Exit();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_IsDBLoaded();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_IsAdmin();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_IsAppData();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_RebuildDB();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_UpdateAllFolderIndexes();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_SaveDB();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_SaveRunHistory();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_DeleteRunHistory();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetTargetMachine();

        // Everything 1.4
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetSort(UInt32 dwSortType);
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetSort();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetResultListSort();
        [DllImport("Everything64.dll")]
        public static extern void Everything_SetRequestFlags(UInt32 dwRequestFlags);
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetRequestFlags();
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetResultListRequestFlags();
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultExtension(UInt32 nIndex);
        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetResultSize(UInt32 nIndex, out long lpFileSize);
        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetResultDateCreated(UInt32 nIndex, out long lpFileTime);
        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetResultDateModified(UInt32 nIndex, out long lpFileTime);
        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetResultDateAccessed(UInt32 nIndex, out long lpFileTime);
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetResultAttributes(UInt32 nIndex);
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultFileListFileName(UInt32 nIndex);
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetResultRunCount(UInt32 nIndex);
        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetResultDateRun(UInt32 nIndex, out long lpFileTime);
        [DllImport("Everything64.dll")]
        public static extern bool Everything_GetResultDateRecentlyChanged(UInt32 nIndex, out long lpFileTime);
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultHighlightedFileName(UInt32 nIndex);
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultHighlightedPath(UInt32 nIndex);
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr Everything_GetResultHighlightedFullPathAndFileName(UInt32 nIndex);
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_GetRunCountFromFileName(string lpFileName);
        [DllImport("Everything64.dll")]
        public static extern bool Everything_SetRunCountFromFileName(string lpFileName, UInt32 dwRunCount);
        [DllImport("Everything64.dll")]
        public static extern UInt32 Everything_IncRunCountFromFileName(string lpFileName);
        #endregion

        const int MAX_PATH = 260;

        public bool Loaded => Everything_IsDBLoaded();

        public uint LastError => Everything_GetLastError();

        public IEnumerable<string> GetFiles(string path, string pattern = "*.*", bool nested = false)
        {
            var result = new List<string>();
            try
            {
                var query = nested ? $"file:{path.TrimEnd('\\')}\\ {pattern}" : $"file:{System.IO.Path.Combine(path, pattern)} parent:{path}";
                
                //Everything_Reset();
                Everything_SetSearchW(query);
                Everything_SetMatchPath(true);
                Everything_SetRequestFlags(EVERYTHING_REQUEST_FILE_NAME | EVERYTHING_REQUEST_PATH | EVERYTHING_REQUEST_DATE_MODIFIED | EVERYTHING_REQUEST_SIZE);
                Everything_SetSort(EVERYTHING_SORT_NAME_ASCENDING);
                Everything_QueryW(true);

                var count = Everything_GetNumResults();
                for (uint i = 0; i < count; i++)
                {
                    var fullpath = new StringBuilder(MAX_PATH);
                    Everything_GetResultFullPathName(i, fullpath, MAX_PATH);
                    result.Add(fullpath.ToString().Replace(path, "").TrimStart('\\'));
                    //var f = Marshal.PtrToStringUni(Everything_GetResultFileName(i));
                    //result.Add(Marshal.PtrToStringUni(Everything_GetResultFileName(i)));
                }
            }
            catch (Exception ex){ ex.ERROR("EverythingGetFiles"); }
            return (result);
        }

        public IEnumerable<string> EnumerateFiles(string path, string pattern = "*.*", bool nested = false)
        {
            var result = new List<string>();
            try
            {
                var query = nested ? $"file:{path.TrimEnd('\\')}\\ {pattern}" : $"file:{System.IO.Path.Combine(path, pattern)} parent:{path}";

                //Everything_Reset();
                Everything_SetSearchW(query);
                Everything_SetMatchPath(true);
                Everything_SetRequestFlags(EVERYTHING_REQUEST_FILE_NAME | EVERYTHING_REQUEST_PATH | EVERYTHING_REQUEST_DATE_MODIFIED | EVERYTHING_REQUEST_SIZE);
                Everything_SetSort(EVERYTHING_SORT_NAME_ASCENDING);
                Everything_QueryW(true);

            }
            catch (Exception ex) { ex.ERROR("EverythingGetFiles"); }

            var count = Everything_GetNumResults();
            for (uint i = 0; i < count; i++)
            {
                var fullpath = new StringBuilder(MAX_PATH);
                Everything_GetResultFullPathName(i, fullpath, MAX_PATH);
                yield return (fullpath.ToString().Replace(path, "").TrimStart('\\'));
            }
        }

        public bool FileExists(string path, string pattern = "*.*", bool nested = false)
        {
            var result = false;
            try
            {
                var files = GetFiles(path, pattern, nested);
                result = files.Any();
            }
            catch (Exception ex)
            {
                // Handle the exception as needed
                //Console.WriteLine($"Error checking file existence: {ex.Message}");
                ex.ERROR("EverythingFileExists");
            }
            return (result);
        }
    }
}
