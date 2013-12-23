using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinAlfred.Helper
{
    public class WinAlfredException : Exception
    {
        public WinAlfredException(string msg)
            : base(msg)
        {

        }
    }
}
