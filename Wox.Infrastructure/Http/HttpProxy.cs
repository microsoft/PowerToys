using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin;

namespace Wox.Infrastructure.Http
{
    public class HttpProxy : IHttpProxy
    {
        private static readonly HttpProxy instance = new HttpProxy();

        private HttpProxy()
        {
        }

        public static HttpProxy Instance
        {
            get { return instance; }
        }

        public bool Enabled
        {
            get { return UserSettingStorage.Instance.ProxyEnabled; }
        }

        public string Server
        {
            get { return UserSettingStorage.Instance.ProxyServer; }
        }

        public int Port
        {
            get { return UserSettingStorage.Instance.ProxyPort; }
        }

        public string UserName
        {
            get { return UserSettingStorage.Instance.ProxyUserName; }
        }

        public string Password
        {
            get { return UserSettingStorage.Instance.ProxyPassword; }
        }
    }
}