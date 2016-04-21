using System.IO;

namespace Wox.Infrastructure.Storage
{
    public class PluginSettingsStorage<T> :JsonStrorage<T> where T : new()
    {
        public PluginSettingsStorage()
        {
            var pluginDirectoryName = "Plugins";

            // C# releated, add python releated below
            var type = typeof (T);
            FileName = type.Name;
            var assemblyName = type.Assembly.GetName().Name;
            DirectoryPath = Path.Combine(WoxDirectroy.Executable, DirectoryName, pluginDirectoryName, assemblyName);

            FilePath = Path.Combine(DirectoryPath, FileName + FileSuffix);
        }
    }
}
