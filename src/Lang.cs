using System;
using System.Globalization;

namespace QuickTile
{
    public static class Lang
    {
        private static bool _isEnglish;

        static Lang()
        {
            SetLanguage(null);
        }

        public static bool IsEnglish
        {
            get { return _isEnglish; }
        }

        public static string CurrentCode
        {
            get { return _isEnglish ? "en" : "zh"; }
        }

        public static void SetLanguage(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                string ui = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                _isEnglish = !string.Equals(ui, "zh", StringComparison.OrdinalIgnoreCase);
                return;
            }

            string norm = code.Trim().ToLowerInvariant();
            if (norm == "en" || norm.StartsWith("en-"))
            {
                _isEnglish = true;
            }
            else if (norm == "zh" || norm.StartsWith("zh-"))
            {
                _isEnglish = false;
            }
        }

        public static string T(string zh, string en)
        {
            return _isEnglish ? en : zh;
        }
    }
}
