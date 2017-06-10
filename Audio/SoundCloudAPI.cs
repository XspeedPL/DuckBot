using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DuckBot.Audio
{
    internal static class SoundCloudAPI
    {
        private const string CLIENT_ID = "client_id=cd3e093bf9688f09e3cdf15565fed8f3";
        
        public static async Task<(string, string, string)> Search(string song)
        {
            using (HttpClient hc = new HttpClient())
            {
                hc.DefaultRequestHeaders.AcceptEncoding.Add(System.Net.Http.Headers.StringWithQualityHeaderValue.Parse("utf-8"));
                List<ResultItem> res = null;
                string data = null;
                int i = -1;
                LinkedList<ResultItem> used = new LinkedList<ResultItem>();
                while (++i < 6)
                    try
                    {
                        data = await hc.GetStringAsync("https://api.soundcloud.com/tracks.json?" + CLIENT_ID + "&limit=10&q=" + System.Net.WebUtility.UrlEncode(song));
                        res = new List<ResultItem>(ResultItem.Parse(data));
                        if (res.Count > 1)
                        {
                            foreach (ResultItem item in res)
                                item.diff = Utils.Similarity(song, item.User + " " + item.Title);
                            res.Sort();
                            foreach (ResultItem item in res)
                            {
                                if (used.Contains(item)) continue;
                                else used.AddLast(item);
                                return (string.Format(Resources.Strings.ret_song, item.Title), item.Title, item.URL + "?" + CLIENT_ID);
                            }
                        }
                    }
                    catch (HttpRequestException)
                    {
                        await Task.Delay(500);
                        if (res != null)
                            foreach (ResultItem ri in res)
                                used.Remove(ri);
                    }
                return (string.Format(Resources.Strings.err_nosong, song), null, null);
            }
        }

        private sealed class ResultItem : IComparable<ResultItem>, IEquatable<ResultItem>
        {
            public readonly string User, Title, URL;
            public int diff;

            private ResultItem(string title, string url, string user)
            {
                Title = title; URL = url; User = user; diff = -1;
            }

            public int CompareTo(ResultItem item)
            {
                if (item == null) throw new ArgumentNullException("item");
                int ret = item.diff - diff;
                return ret == 0 ? Title.Length - item.Title.Length : ret;
            }

            public bool Equals(ResultItem item)
            {
                if (item == null) throw new ArgumentNullException("item");
                return User.Equals(item.User, StringComparison.OrdinalIgnoreCase) && Title.Equals(item.Title);
            }

            public static IEnumerable<ResultItem> Parse(string data)
            {
                foreach (string entry in data.Split(new string[] { "\"download_url\"" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string title = Utils.Extract(entry, "\"title\":\"", "\"");
                    string user = Utils.Extract(entry, "\"username\":\"", "\"");
                    if (title != null)
                        yield return new ResultItem(title, Utils.Extract(entry, "\"stream_url\":\"", "\""), user);
                }
            }
        }
    }
}