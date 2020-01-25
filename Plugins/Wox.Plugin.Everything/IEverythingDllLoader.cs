using System;
using System.IO;
using System.Runtime.InteropServices;
using Wox.Infrastructure;

namespace Wox.Plugin.Everything
{
    public interface IEverythingDllLoader
    {
        void Load(PluginInitContext context);
    }

    public class EverythingDllLoader : IEverythingDllLoader
    {
        

        public void Load(PluginInitContext context)
        {
            //var pluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
            //const string sdk = "EverythingSDK";
            //var bundledSDKDirectory = Path.Combine(pluginDirectory, sdk, CpuType());
            //var sdkDirectory = Path.Combine(_storage.DirectoryPath, sdk, CpuType());
            //Helper.ValidateDataDirectory(bundledSDKDirectory, sdkDirectory);

            //var sdkPath = Path.Combine(sdkDirectory, DLL);
            //Constant.EverythingSDKPath = sdkPath;
            //LoadLibrary(sdkPath);
        }

        

        private static string CpuType()
        {
            return Environment.Is64BitOperatingSystem ? "x64" : "x86";
        }
    }
}