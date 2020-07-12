using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Wox.Infrastructure.FileSystemHelper
{
    interface IDirectoryWrapper
    {
        DirectoryInfo GetParent(string path);
    }
}
