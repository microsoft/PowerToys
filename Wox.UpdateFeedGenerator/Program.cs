using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;

namespace Wox.UpdateFeedGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            new Generator().Build();
        }
    }
}
