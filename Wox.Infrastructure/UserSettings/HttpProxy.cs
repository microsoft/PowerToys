namespace Wox.Infrastructure.UserSettings
{
    public class HttpProxy
    {
        public bool Enabled { get; set; } = false;
        public string Server { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}