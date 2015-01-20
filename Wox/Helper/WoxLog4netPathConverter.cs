using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Wox.Helper
{
    public class WoxLog4netPathConverter : log4net.Util.PatternConverter
    {
        protected override void Convert(TextWriter writer, object state)
        {
            string userProfilePath = Environment.GetEnvironmentVariable("USERPROFILE");
            writer.Write(Path.Combine(userProfilePath, ".Wox"));
        }
    }
}
