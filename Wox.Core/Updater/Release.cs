namespace Wox.Core.Updater
{
    public class Release
    {
        public string version { get; set; }
        public string download_link { get; set; }
        public string download_link1 { get; set; }
        public string download_link2 { get; set; }
        public string description { get; set; }

        public override string ToString()
        {
            return version;
        }
    }
}