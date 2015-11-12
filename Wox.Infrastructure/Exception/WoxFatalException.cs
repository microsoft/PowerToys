namespace Wox.Infrastructure.Exception
{
    /// <summary>
    /// Represent exceptions that wox can't handle and MUST close running Wox.
    /// </summary>
    public class WoxFatalException : WoxException
    {
        public WoxFatalException(string msg) : base(msg)
        {
        }
    }
}
