// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;
using System.Text.Json;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Wox.Infrastructure.Storage
{
    public class PluginJsonStorage<T> : JsonStorage<T>
        where T : new()
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;

        public PluginJsonStorage()
        {
            // C# related, add python related below
            var dataType = typeof(T);
            var assemblyName = typeof(T).Assembly.GetName().Name;
            DirectoryPath = Path.Combine(Constant.DataDirectory, DirectoryName, Constant.PluginsDataStorage, assemblyName);
            Helper.ValidateDirectory(DirectoryPath);

            FilePath = Path.Combine(DirectoryPath, $"{dataType.Name}{FileSuffix}");
        }

        public override T Load()
        {
            var data = base.Load();

            // Check information file for version mismatch
            try
            {
                if (CheckVersionMismatch())
                {
                    if (!TryLoadData())
                    {
                        Clear();
                    }
                }
            }
            catch (JsonException e)
            {
                Log.Exception($"Error in Load of PluginJsonStorage: {e.Message}", e, GetType());
            }

            return data.NonNull();
        }
    }
}
