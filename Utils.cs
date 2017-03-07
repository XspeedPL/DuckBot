using System;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;
using System.Resources;
using Discord;

namespace DuckBot
{
    public static class Utils
    {
        public static Task RunAsync<T>(Action<T> func, T arg)
        {
            return Task.Run(() => func(arg));
        }

        public static string StartCase(this string s)
        {
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        public static bool IsCultureAvailable(string langCode)
        {
            CultureInfo ci = null;
            try { ci = CultureInfo.GetCultureInfo(langCode); }
            catch (CultureNotFoundException) { return false; }
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

        public static bool UserActive(this User u) { return u.Status == UserStatus.Online || u.Status == UserStatus.DoNotDisturb; }

        public static int Similarity(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;
            a = a.ToLower(); b = b.ToLower();
            int pos1 = 0, pos2 = 0, ret = 0;
            while (pos1 < a.Length && pos2 < b.Length)
                if (a[pos1] == b[pos2])
                {
                    ++pos1; ++pos2; ++ret;
                }
                else if (a.Length - pos1 > b.Length - pos2) ++pos1;
                else ++pos2;
            return ret;
        }

        public static string ReplaceIC(this string input, string search, string replace)
        {
            int ix = -replace.Length;
            while ((ix = input.IndexOf(search, ix + replace.Length, StringComparison.OrdinalIgnoreCase)) != -1)
                input = input.Remove(ix, search.Length).Insert(ix, replace);
            return input;
        }

        public static string XReplaceIC(this string input, params string[] search)
        {
            foreach (string s in search) input = input.ReplaceIC(s, "");
            return input;
        }

        public static string Extract(string input, string start, string end)
        {
            int ix = input.LastIndexOf(start);
            if (ix == -1) return null;
            ix += start.Length;
            int ie = input.IndexOf(end, ix);
            if (ie == -1) return null;
            return input.Substring(ix, ie - ix);
        }

        public static string BracketFilter(string input)
        {
            if (input.IndexOfAny(new char[] { '(', ')' }) == -1) return input;
            StringBuilder sb = new StringBuilder(input);
            bool f = false;
            for (int i = 0; i < sb.Length; ++i)
                if (sb[i] == '(')
                {
                    if (f) sb.Remove(i--, 1);
                    else f = true;
                }
                else if (sb[i] == ')')
                {
                    if (f) f = false;
                    else sb.Remove(i--, 1);
                }
            input = sb.ToString();
            return f ? sb.Remove(input.LastIndexOf('('), 1).ToString() : input;
        }

        public static int IndexOfIC(this string input, string search)
        {
            return input.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        }

        public static string[] SplitIC(this string input, params string[] delim)
        {
            foreach (string s in delim) input = input.ReplaceIC(s, s);
            return input.Split(delim, StringSplitOptions.None);
        }
    }
}
