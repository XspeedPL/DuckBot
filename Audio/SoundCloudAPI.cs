using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Web;

namespace DuckBot.Audio
{
    internal static class SoundCloudAPI
    {
        internal static readonly string CLIENT_ID = "client_id=cd3e093bf9688f09e3cdf15565fed8f3";

        internal static readonly Regex REX_ALPHA = new Regex("[^a-zA-Z0-9 &'.()-]");
        internal static readonly Regex REX_BRCTS = new Regex("(\\([\\s0-9]*\\))");

        public static string Search(string song, out Song songData)
        {
            string tmp = song.Replace(" - ", " ").ReplaceIC(" x ", " ").ReplaceIC(" ft ", " ").ReplaceIC(" vs ", " ").ReplaceIC(" i ", " ").ReplaceIC(" a ", " ").ReplaceIC(" mr ", " ").Replace("  ", " ").Trim();
            string conv = HttpUtility.UrlEncode(REX_ALPHA.Replace(tmp.Replace(" & ", " ").Replace("'", ""), ""));
            using (WebClient wc = new WebClient())
            {
                List<ResultItem> res = null;
                string data = null;
                int i = -1;
                LinkedList<ResultItem> used = new LinkedList<ResultItem>();
                while (++i < 6)
                    try
                    {
                        if (i == 3 && song.Contains(" & "))
                        {
                            conv = REX_ALPHA.Replace(tmp.Replace(" & ", " and ").Replace("'", ""), "").Replace("  ", " ").Trim();
                            conv = HttpUtility.UrlEncode(conv);
                        }
                        if (i == 4 && song.Contains("'"))
                        {
                            conv = REX_ALPHA.Replace(tmp, "").Replace("  ", " ").Trim();
                            conv = HttpUtility.UrlEncode(conv);
                        }
                        if (i == 5 && song.Contains("'") && song.Contains(" & "))
                        {
                            conv = REX_ALPHA.Replace(tmp.Replace(" & ", " and "), "").Replace("  ", " ").Trim();
                            conv = HttpUtility.UrlEncode(conv);
                        }
                        data = Encoding.UTF8.GetString(wc.DownloadData("https://api.soundcloud.com/tracks.json?" + CLIENT_ID + "&limit=10&q=" + conv));
                        res = new List<ResultItem>(ResultItem.Parse(data));
                        if (res.Count > 1)
                        {
                            foreach (ResultItem item in res)
                                item.diff = Utils.Similarity(song, item.Data.Full) - Math.Abs(item.Data.Full.Length - song.Length) / 2;
                            res.Sort();
                            foreach (ResultItem item in res)
                            {
                                if (used.Contains(item)) continue;
                                else used.AddLast(item);
                                if (item.Data.Title.StartsWith("Monstercat")) continue;
                                songData = item.Data;
                                return "Found '" + item.Data.Full + "'";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Threading.Thread.Sleep(500);
                        if (ex is WebException)
                            foreach (ResultItem ri in res)
                                used.Remove(ri);
                    }
                songData = null;
                return "Track '" + song + "' not found!\r\n   Query: " + conv;
            }
        }

        internal sealed class ResultItem : IComparable<ResultItem>, IEquatable<ResultItem>
        {
            public readonly string User;
            public readonly Song Data;
            public int diff;

            private ResultItem(Song data, string user)
            {
                Data = data; User = user; diff = -1;
            }

            public int CompareTo(ResultItem ri)
            {
                int ret = ri.diff - diff;
                return ret == 0 ? Data.Full.Length - ri.Data.Full.Length : ret;
            }

            public bool Equals(ResultItem ri)
            {
                return User.Equals(ri.User, StringComparison.OrdinalIgnoreCase) && Data.Equals(ri.Data);
            }

            public static IEnumerable<ResultItem> Parse(string data)
            {
                foreach (string entry in data.Split(new string[] { "\"download_url\"" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string title = Utils.Extract(entry, "\"title\":\"", "\"");
                    string user = Utils.Extract(entry, "\"username\":\"", "\"");
                    if (title != null)
                    {
                        Song res = ParseSong(title, user, false);
                        if (res != null)
                        {
                            res.URL = Utils.Extract(entry, "\"stream_url\":\"", "\"");
                            yield return new ResultItem(res, user);
                        }
                    }
                }
            }

            public static Song ParseSong(string title, string altartist, bool issource)
            {
                Song ret = new Song();
                try
                {
                    title = title.Replace('ú', 'u').Replace('ä', 'a').Replace('é', 'e').Replace('[', '(').Replace(']', ')').Replace(")(", ") (").Trim();
                    if (title.IndexOfIC("(sample)") != -1) return null;
                    if (title.StartsWith("(")) title = title.Substring(title.IndexOf(')') + 1).Trim();
                    if (title[title.Length - 2] == ')') title = title.Remove(title.Length - 1);
                    title = title.ReplaceIC("monsercat", "monstercat").ReplaceIC("monstecat", "monstercat");
                    if (issource)
                        title = title.XReplaceIC("exclusive preview", "monstercat preview");
                    title = title.XReplaceIC("monstercat premiere", "monstercat spotlight", "monstercat throwback",
                        "monstercat release", "monstercat exclusive", "in description", "podcast premiere", "original radio edit",
                        "ep release", "free download", "original mix", "available on", "beatport", "monstercat", "radio edit",
                        "podcast edit", "out now on", "out now!", "(out now)", "(unreleased)", "(reprise)", "(intro)", "(vip)");
                    title = title.ReplaceIC(" x ", " & ").Replace('’', '\'');
                    title = title.ReplaceIC("feat", "ft").Replace(", ", " & ").Replace(". ", " ").ReplaceIC("Mr.F", "Mr F");
                    title = title.ReplaceIC("(ft ", "ft ").ReplaceIC(" vs ", " & ").ReplaceIC("Man & Machines", "Men & Machines");
                    title = title.ReplaceIC("Stereotonique", "Stereotronique").ReplaceIC("Scorprion", "Scorpion");
                    title = title.ReplaceIC("McLoughin", "McLoughlin").ReplaceIC("Charlie Brix", "Charli Brix");
                    title = title.ReplaceIC("Klapex", "Klaypex").ReplaceIC("Roufailda", "Roufaida");
                    title = title.ReplaceIC("Greenstorm", "Green Storm").ReplaceIC("Zuehisdorff", "Zuehlsdorff");
                    title = title.ReplaceIC("Tylor", "Taylor").ReplaceIC("Johnny", "Jonny").ReplaceIC("Case & Point", "Case '&' Point");
                    title = title.ReplaceIC("Gamble & Burke", "Gamble '&' Burke").ReplaceIC("Strongarm", "Strong Arm");
                    title = title.ReplaceIC("Slips & Slurs", "Slips '&' Slurs").Trim().Replace("  ", " ");
                    if (title[0] == '-') title = title.Substring(1).Trim();
                    int ix = title.IndexOf(" | ");
                    if (ix != -1)
                    {
                        if (title.Contains(" - ")) title = title.Remove(ix);
                        else title = title.Replace(" | ", " - ");
                    }
                    title = Utils.BracketFilter(title);
                    ix = title.IndexOf(" - ");
                    if (ix != title.LastIndexOf(" - "))
                    {
                        title = title.Remove(ix) + " -|- " + title.Substring(ix + 3);
                        title = title.Replace(" - ", " ").Replace(" -|- ", " - ");
                    }
                    string[] parts = title.SplitIC(ix != -1 ? " - " : " by ", " ft ");
                    string meta = parts[parts.Length - 1];
                    ix = meta.IndexOf(" (");
                    if (ix != -1)
                    {
                        meta = REX_ALPHA.Replace(meta.Substring(ix), "");
                        parts[parts.Length - 1] = parts[parts.Length - 1].Remove(ix);
                    }
                    else meta = "";
                    int i2 = title.IndexOfIC(" ft ");
                    if ((ix = title.IndexOf(" - ")) != -1)
                    {
                        ret.Artist = parts[0];
                        if (i2 == -1) ret.Title = parts[1];
                        else if (i2 > ix)
                        {
                            ret.Title = parts[1];
                            ret.Feat = parts[2];
                        }
                        else
                        {
                            ret.Title = parts[2];
                            ret.Feat = parts[1];
                        }
                    }
                    else if ((ix = title.IndexOfIC(" by ")) != -1)
                    {
                        ret.Title = parts[0];
                        if (i2 == -1) ret.Artist = parts[1];
                        else if (i2 > ix) { ret.Artist = parts[1]; ret.Feat = parts[2]; }
                        else { ret.Artist = parts[2]; ret.Feat = parts[1]; }
                    }
                    else if (altartist != null)
                    {
                        ret.Artist = altartist;
                        ret.Title = parts[0];
                        if (i2 != -1) ret.Artist += " & " + parts[1];
                    }
                    else return null;
                    ret.Artist = REX_ALPHA.Replace(ret.Artist, "").Replace(" & & ", " & ").Trim();
                    if (ret.Artist.EndsWith(" &")) ret.Artist = ret.Artist.Remove(ret.Artist.Length - 2);
                    if (ret.Artist.StartsWith("& ")) ret.Artist = ret.Artist.Substring(2);
                    ret.Feat = REX_ALPHA.Replace(ret.Feat, "").Trim();
                    ret.Title = REX_BRCTS.Replace(REX_ALPHA.Replace(ret.Title, "") + meta, "").Replace("  ", " ").Trim();
                    return ret;
                }
                catch { return null; }
            }
        }
    }
}