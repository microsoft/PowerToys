using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wox.Infrastructure.Storage
{
    class WoxJsonStorage<T> : JsonStrorage<T> where T : new()
    {
        public WoxJsonStorage()
        {
            var directoryPath = Path.Combine(Constant.DataDirectory, DirectoryName);
            Helper.ValidateDirectory(directoryPath);

            var filename = typeof(T).Name;
            FilePath = Path.Combine(directoryPath, $"{filename}{FileSuffix}");
        }
    }
}
