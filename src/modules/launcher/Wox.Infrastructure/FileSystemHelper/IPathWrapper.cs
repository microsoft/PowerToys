using System;
using System.Collections.Generic;
using System.Text;

namespace Wox.Infrastructure.Storage
{
    interface IPathWrapper
    {
        string GetFileNameWithoutExtension(string path);
        string GetFileName(string path);
    }
}
