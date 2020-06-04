using System.IO;

namespace Wox.Infrastructure.Storage
{
    public class PluginJsonStorage<T> :JsonStorage<T> where T : new()
    {
        public PluginJsonStorage()
        {
            // C# related, add python related below
            var dataType = typeof(T);
            var assemblyName = typeof(T).Assembly.GetName().Name;
            DirectoryPath = Path.Combine(Constant.DataDirectory, DirectoryName, Constant.Plugins, assemblyName);
            Helper.ValidateDirectory(DirectoryPath);

            FilePath = Path.Combine(DirectoryPath, $"{dataType.Name}{FileSuffix}");
        }
    }
}
