namespace Wox.Core.Exception
{
    /// <summary>
    /// Base Wox Exceptions
    /// </summary>
    public class WoxException : System.Exception
    {
        public WoxException(string msg)
            : base(msg)
        {

        }

        public WoxException(string msg, System.Exception innerException)
            : base(msg, innerException)
        {
        }
    }
}
