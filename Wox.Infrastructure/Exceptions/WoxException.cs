using System;

namespace Wox.Infrastructure.Exceptions
{
    public class WoxException : Exception
    {
        public WoxException(string msg)
            : base(msg)
        {

        }
    }
}
