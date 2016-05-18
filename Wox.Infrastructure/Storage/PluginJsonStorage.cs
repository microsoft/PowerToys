using System.IO;

namespace Wox.Infrastructure.Storage
{
    public class PluginJsonStorage<T> :JsonStrorage<T> where T : new()
    {
        public PluginJsonStorage()
        {
            DirectoryName = Constant.Plugins;
            
            // C# releated, add python releated below
            var assemblyName = DataType.Assembly.GetName().Name;
            DirectoryPath = Path.Combine(DirectoryPath, DirectoryName, assemblyName);
            FilePath = Path.Combine(DirectoryPath, FileName + FileSuffix);

            ValidateDirectory();
        }
    }
}
