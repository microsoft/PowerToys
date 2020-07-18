using System;
using System.Collections.Generic;
using System.Text;

namespace Wox.Infrastructure.FileSystemHelper
{
    public interface IFileWrapper
    {
        string[] ReadAllLines(string path);
    }
}
