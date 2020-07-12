using System;
using System.Collections.Generic;
using System.Text;

namespace Wox.Infrastructure.FileSystemHelper
{
    interface IFileWrapper
    {
        string[] ReadAllLines(string path);
    }
}
