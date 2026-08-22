using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ConfigurO
{
    /// <summary>
    /// Thin read layer over the translation file loaded by
    /// <see cref="OptionsHelper.LoadTranslation"/>.
    ///
    /// The legacy UI indexed the dynamic JObject directly, which throws on a
    /// missing key -- fine when every key was guaranteed by the designer, but
    /// not once new strings (the Windows 11 tweaks, the redesigned shell) exist
    /// ahead of their translations. Everything here falls back to the supplied
    /// English text instead.
    /// </summary>
    internal static class I18n
    {
        /// <summary>
        /// Looks up <paramref name="key"/>, returning <paramref name="fallback"/>
        /// when the key is absent, blank, or the translation files failed to load.
        /// </summary>
        internal static string Get(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            try
            {
                JObject list = OptionsHelper.TranslationList as JObject;
                if (list == null) return fallback;
                JToken t;
                if (!list.TryGetValue(key, out t)) return fallback;
                string v = t == null ? null : t.ToString();
                return string.IsNullOrWhiteSpace(v) ? fallback : v;
            }
            catch (Exception ex)
            {
                Logger.LogError("I18n.Get:" + key, ex.Message, ex.StackTrace);
                return fallback;
            }
        }

        /// <summary>Lookup with the key itself as the last resort.</summary>
        internal static string Get(string key)
        {
            return Get(key, key);
        }

        /// <summary>
        /// Every translated string as a plain dictionary, empty when nothing is
        /// loaded.
        ///
        /// Callers used to reach TranslationList.ToObject&lt;...&gt;() directly,
        /// which throws when the translations failed to load -- and those calls
        /// sit in dialogs opened to report a problem, so the failure surfaced as
        /// a crash instead of the message it was trying to show. An empty map
        /// leaves the designer's English in place, which is the right outcome.
        /// </summary>
        internal static Dictionary<string, string> Map()
        {
            try
            {
                JObject list = OptionsHelper.TranslationList as JObject;
                if (list == null) return new Dictionary<string, string>();
                return list.ToObject<Dictionary<string, string>>()
                       ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Logger.LogError("I18n.Map", ex.Message, ex.StackTrace);
                return new Dictionary<string, string>();
            }
        }
    }
}
