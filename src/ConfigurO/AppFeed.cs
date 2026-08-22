using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace ConfigurO
{
    /// <summary>
    /// The app-downloader catalogue: entries from feed/feed.json and their
    /// icons from feed/icons.zip.
    ///
    /// Both are cached under the ConfigurO data folder after the first fetch,
    /// so the screen opens instantly on later runs and still works offline.
    /// Entries with no download link yet are kept in the catalogue and flagged
    /// through <see cref="IsAvailable"/> rather than silently dropped.
    /// </summary>
    internal static class AppFeed
    {
        const string FeedUrl = "https://raw.githubusercontent.com/wrstt/ConfigurO/main/feed/feed.json";
        const string IconsUrl = "https://raw.githubusercontent.com/wrstt/ConfigurO/main/feed/icons.zip";

        static readonly string CacheFolder = Path.Combine(CoreHelper.CoreFolder, "Feed");
        static readonly string FeedCache = Path.Combine(CacheFolder, "feed.json");
        static readonly string IconCache = Path.Combine(CacheFolder, "icons.zip");

        static readonly Dictionary<string, Image> _icons =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        internal static List<AppInfo> Apps = new List<AppInfo>();

        internal static bool Loaded { get; private set; }

        /// <summary>True when the entry has at least one usable download link.</summary>
        internal static bool IsAvailable(AppInfo a)
        {
            return a != null && (!string.IsNullOrEmpty(a.Link) || !string.IsNullOrEmpty(a.Link64));
        }

        internal static IEnumerable<string> Categories()
        {
            // Preserve the catalogue's own order rather than sorting.
            List<string> seen = new List<string>();
            foreach (AppInfo a in Apps)
                if (!string.IsNullOrEmpty(a.Group) && !seen.Contains(a.Group)) seen.Add(a.Group);
            return seen;
        }

        internal static IEnumerable<AppInfo> InCategory(string category)
        {
            return Apps.Where(a => a.Group == category);
        }

        /// <summary>
        /// Loads the catalogue. Runs on a worker thread; call
        /// <see cref="Icon"/> only after this returns.
        /// </summary>
        internal static void Load(bool forceRefresh)
        {
            try
            {
                Directory.CreateDirectory(CacheFolder);
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                string json = Fetch(FeedUrl, FeedCache, forceRefresh);
                if (!string.IsNullOrEmpty(json))
                    Apps = JsonConvert.DeserializeObject<List<AppInfo>>(json) ?? new List<AppInfo>();

                LoadIcons(forceRefresh);
                Loaded = Apps.Count > 0;
            }
            catch (Exception ex)
            {
                Logger.LogError("AppFeed.Load", ex.Message, ex.StackTrace);
                Loaded = false;
            }
        }

        static string Fetch(string url, string cache, bool forceRefresh)
        {
            if (!forceRefresh && File.Exists(cache))
            {
                try { return File.ReadAllText(cache); }
                catch (Exception ex) { Logger.LogError("AppFeed.Fetch-cache", ex.Message, ex.StackTrace); }
            }
            try
            {
                using (WebClient c = new WebClient { Encoding = Encoding.UTF8 })
                {
                    c.Headers.Add("Cache-Control", "no-cache");
                    string body = c.DownloadString(url);
                    File.WriteAllText(cache, body);
                    return body;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("AppFeed.Fetch", ex.Message, ex.StackTrace);
                // A stale cache beats an empty screen.
                return File.Exists(cache) ? File.ReadAllText(cache) : null;
            }
        }

        static void LoadIcons(bool forceRefresh)
        {
            if (forceRefresh || !File.Exists(IconCache))
            {
                try
                {
                    using (WebClient c = new WebClient())
                    {
                        c.Headers.Add("Cache-Control", "no-cache");
                        c.DownloadFile(IconsUrl, IconCache);
                    }
                }
                catch (Exception ex) { Logger.LogError("AppFeed.LoadIcons-download", ex.Message, ex.StackTrace); }
            }
            if (!File.Exists(IconCache)) return;

            foreach (Image i in _icons.Values) i.Dispose();
            _icons.Clear();

            try
            {
                using (FileStream fs = File.OpenRead(IconCache))
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        if (!entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                        try
                        {
                            // Copy out first: Image keeps the stream alive, and
                            // the archive's is not seekable.
                            using (Stream s = entry.Open())
                            using (MemoryStream ms = new MemoryStream())
                            {
                                s.CopyTo(ms);
                                ms.Position = 0;
                                _icons[entry.Name] = Image.FromStream(ms);
                            }
                        }
                        catch (Exception ex) { Logger.LogError("AppFeed.Icon:" + entry.Name, ex.Message, ex.StackTrace); }
                    }
                }
            }
            catch (Exception ex) { Logger.LogError("AppFeed.LoadIcons", ex.Message, ex.StackTrace); }
        }

        /// <summary>The app's icon, or null when the feed has none for it.</summary>
        internal static Image Icon(AppInfo a)
        {
            if (a == null || string.IsNullOrEmpty(a.Image)) return null;
            string file = a.Image.Substring(a.Image.LastIndexOf('/') + 1);
            Image image;
            return _icons.TryGetValue(file, out image) ? image : null;
        }

        /// <summary>Phosphor-style fallback glyph for a category with no icon.</summary>
        internal static string CategoryIcon(string category)
        {
            switch (category)
            {
                case "Web Browsers": return "global-line";
                case "Messaging": return "chat-3-line";
                case "Media": return "play-circle-line";
                case ".NET": return "stack-line";
                case "Java": return "cup-line";
                case "Imaging": return "image-line";
                case "Documents": return "file-list-line";
                case "Security": return NocturneIcons.Shield;
                case "Compression": return "file-zip-line";
                case "File Sharing": return "links-line";
                case "Online Storage": return "cloud-line";
                case "VC++ Redistributables": return "box-3-line";
                case "Developer Tools": return "code-s-slash-line";
                case "Utilities": return "tools-line";
                default: return "apps-line";
            }
        }
    }
}
