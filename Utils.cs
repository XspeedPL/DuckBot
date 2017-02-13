using System.Collections.Generic;
using System.Globalization;
using System.Resources;

namespace DuckBot
{
    public static class Utils
    {
        public static string StartCase(this string s)
        {
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        public static bool IsCultureAvailable(string langCode)
        {
            CultureInfo ci = null;
            try { ci = CultureInfo.GetCultureInfo(langCode); }
            catch { return false; }
            ResourceManager rm = null;
            try
            {
                rm = new ResourceManager(typeof(Resources.Strings));
                using (ResourceSet rs = rm.GetResourceSet(ci, true, false))
                    if (rs != null) return true;
                return false;
            }
            finally { rm.ReleaseAllResources(); }
        }
    }
}
