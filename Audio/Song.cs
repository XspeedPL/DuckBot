namespace DuckBot.Audio
{
    public class Song
    {
        public Song()
        {
            Artist = "";
            Feat = "";
            Title = "";
            URL = "";
        }

        public string Full => AllArtists + " - " + Title;

        public string AllArtists => string.IsNullOrEmpty(Feat) ? Artist : Artist + " ft. " + Feat;

        public string Artist { get; set; }

        public string Feat { get; set; }

        public string Title { get; set; }

        public string URL { get; set; }
    }
}
