namespace Wox.Plugin
{
    public interface IHttpProxy
    {
        bool Enabled { get; }
        string Server { get; }
        int Port { get; }
        string UserName { get; }
        string Password { get; }
    }
}