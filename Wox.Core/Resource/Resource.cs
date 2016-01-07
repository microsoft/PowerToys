using System.IO;
using System.Windows;
using Wox.Infrastructure;

namespace Wox.Core.Resource
{
    public abstract class Resource
    {
        public string DirectoryName { get; protected set; }

        protected string DirectoryPath => Path.Combine(WoxDirectroy.Executable, DirectoryName);

        public abstract ResourceDictionary GetResourceDictionary();
    }
}
