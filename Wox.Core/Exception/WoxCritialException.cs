namespace Wox.Core.Exception
{
    /// <summary>
    /// Represent exceptions that wox can't handle and MUST close running Wox.
    /// </summary>
    public class WoxCritialException : WoxException
    {
        public WoxCritialException(string msg) : base(msg)
        {
        }
    }
}
