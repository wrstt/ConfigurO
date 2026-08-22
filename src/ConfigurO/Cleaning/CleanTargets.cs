using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ConfigurO
{
    /// <summary>One selectable row on the Cleaner screen.</summary>
    internal sealed class CleanTarget
    {
        internal string Id;
        internal string Icon;
        internal string Label;
        /// <summary>Populates <see cref="CleanHelper.PreviewCleanList"/> for this target.</summary>
        internal Action Scan;
        /// <summary>Set for the Recycle Bin, which is emptied through the shell rather than by path.</summary>
        internal bool IsRecycleBin;

        /// <summary>Paths found by the last scan.</summary>
        internal List<string> Files = new List<string>();
        internal ByteSize Size = new ByteSize(0);
        internal bool Selected;
    }

    /// <summary>
    /// Scans the cleanable locations, keeping a per-target file list and size.
    ///
    /// <see cref="CleanHelper"/> accumulates everything into one shared list
    /// and one running total, which is all the old single-button cleaner
    /// needed. The redesigned screen shows a size against each row, so each
    /// target is scanned in isolation and the shared state captured and reset
    /// around it.
    /// </summary>
    internal static class CleanTargets
    {
        [DllImport("shell32.dll")]
        static extern int SHQueryRecycleBin(string rootPath, ref SHQUERYRBINFO info);

        [StructLayout(LayoutKind.Sequential)]
        struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        internal static List<CleanTarget> Build()
        {
            return new List<CleanTarget>
            {
                new CleanTarget { Id = "temp",    Icon = "file-3-line",  Label = I18n.Get("cleanTemp", "Temporary files"),
                                  Scan = CleanHelper.PreviewTemp, Selected = true },
                new CleanTarget { Id = "bin",     Icon = NocturneIcons.Trash,    Label = I18n.Get("cleanBin", "Recycle Bin"),
                                  Scan = null, IsRecycleBin = true, Selected = true },
                new CleanTarget { Id = "bsod",    Icon = "skull-line",           Label = I18n.Get("cleanDumps", "BSOD memory dumps"),
                                  Scan = CleanHelper.PreviewMinidumps },
                new CleanTarget { Id = "reports", Icon = NocturneIcons.Warning,  Label = I18n.Get("cleanReports", "Windows error reports"),
                                  Scan = CleanHelper.PreviewErrorReports, Selected = true },
                new CleanTarget { Id = "chrome",  Icon = "chrome-line",          Label = I18n.Get("cleanChrome", "Chrome cache & cookies"),
                                  Scan = () => CleanHelper.PreviewChromeClean(true, true, false, false, false), Selected = true },
                new CleanTarget { Id = "edge",    Icon = "earth-line",           Label = I18n.Get("cleanEdge", "Edge cache & session"),
                                  Scan = () => CleanHelper.PreviewEdgeClean(true, true, false, true) },
                new CleanTarget { Id = "firefox", Icon = "fire-line",            Label = I18n.Get("cleanFirefox", "Firefox cache"),
                                  Scan = () => CleanHelper.PreviewFireFoxClean(true, false, false) },
                new CleanTarget { Id = "ie",      Icon = "ghost-line",           Label = I18n.Get("cleanIE", "Internet Explorer cache"),
                                  Scan = CleanHelper.PreviewInternetExplorerCache }
            };
        }

        /// <summary>
        /// Fills in <see cref="CleanTarget.Files"/> and
        /// <see cref="CleanTarget.Size"/> for every target. Runs off the UI
        /// thread -- a cold temp folder can take several seconds to walk.
        /// </summary>
        internal static void Scan(List<CleanTarget> targets)
        {
            foreach (CleanTarget t in targets)
            {
                t.Files.Clear();
                t.Size = new ByteSize(0);

                if (t.IsRecycleBin)
                {
                    t.Size = new ByteSize(RecycleBinBytes());
                    continue;
                }

                // Capture this target's contribution to the shared state.
                CleanHelper.PreviewCleanList = new List<string>();
                CleanHelper.PreviewSizeToBeFreed = new ByteSize(0);
                try
                {
                    if (t.Scan != null) t.Scan();
                }
                catch (Exception ex)
                {
                    Logger.LogError("CleanTargets.Scan:" + t.Id, ex.Message, ex.StackTrace);
                }
                t.Files = CleanHelper.PreviewCleanList;
                t.Size = CleanHelper.PreviewSizeToBeFreed;

                // Browser previews only build a file list, so total them here.
                if (t.Size.Bytes == 0 && t.Files.Count > 0)
                {
                    ByteSize total = new ByteSize(0);
                    foreach (string f in t.Files) total += CleanHelper.CalculateSize(f);
                    t.Size = total;
                }
            }

            CleanHelper.PreviewCleanList = new List<string>();
            CleanHelper.PreviewSizeToBeFreed = new ByteSize(0);
        }

        /// <summary>Deletes everything the selected targets found. Returns the size freed.</summary>
        internal static ByteSize Clean(IEnumerable<CleanTarget> selected)
        {
            ByteSize freed = new ByteSize(0);
            List<string> files = new List<string>();

            foreach (CleanTarget t in selected)
            {
                freed += t.Size;
                if (t.IsRecycleBin)
                {
                    try { CleanHelper.EmptyRecycleBin(); }
                    catch (Exception ex) { Logger.LogError("CleanTargets.Bin", ex.Message, ex.StackTrace); }
                    continue;
                }
                files.AddRange(t.Files);
            }

            CleanHelper.PreviewCleanList = files;
            try { CleanHelper.Clean(); }
            catch (Exception ex) { Logger.LogError("CleanTargets.Clean", ex.Message, ex.StackTrace); }
            CleanHelper.PreviewCleanList = new List<string>();

            return freed;
        }

        static long RecycleBinBytes()
        {
            try
            {
                SHQUERYRBINFO info = new SHQUERYRBINFO();
                info.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
                // A null root queries every drive's bin at once.
                return SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : 0L;
            }
            catch (Exception ex)
            {
                Logger.LogError("CleanTargets.RecycleBinBytes", ex.Message, ex.StackTrace);
                return 0L;
            }
        }
    }
}
