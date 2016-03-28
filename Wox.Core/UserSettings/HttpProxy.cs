using Wox.Plugin;

namespace Wox.Core.UserSettings
{
    public class HttpProxy : IHttpProxy
    {
        private static readonly HttpProxy instance = new HttpProxy();
        public UserSettingStorage Settings { get; set; }
        public static HttpProxy Instance => instance;

        public bool Enabled => Settings.ProxyEnabled;
        public string Server => Settings.ProxyServer;
        public int Port => Settings.ProxyPort;
        public string UserName => Settings.ProxyUserName;
        public string Password => Settings.ProxyPassword;
    }
}