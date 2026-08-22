using System;
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
    }
}
