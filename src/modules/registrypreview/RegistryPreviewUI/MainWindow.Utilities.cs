// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using WinUIEx;

namespace RegistryPreview
{
    public sealed partial class MainWindow : WindowEx
    {
        private void OpenWindowPlacementFile(string path, string filename)
        {
            string fileContents = string.Empty;
            string storageFile = Path.Combine(path, filename);
            if (File.Exists(storageFile))
            {
                try
                {
                    StreamReader reader = new StreamReader(storageFile);
                    fileContents = reader.ReadToEnd();
                    reader.Close();
                }
                catch
                {
                    // set up default JSON blob
                    fileContents = "{ }";
                }
            }
            else
            {
                Task.Run(() => SaveWindowPlacementFile(path, filename)).GetAwaiter().GetResult();
            }

            try
            {
                jsonWindowPlacement = Windows.Data.Json.JsonObject.Parse(fileContents);
            }
            catch
            {
                // set up default JSON blob
                fileContents = "{ }";
                jsonWindowPlacement = Windows.Data.Json.JsonObject.Parse(fileContents);
            }
        }

        /// <summary>
        /// Save the window placement JSON blob out to a local file
        /// </summary>
        private async void SaveWindowPlacementFile(string path, string filename)
        {
            StorageFolder storageFolder = null;
            StorageFile storageFile = null;
            string fileContents = string.Empty;

            try
            {
                storageFolder = await StorageFolder.GetFolderFromPathAsync(path);
            }
            catch (FileNotFoundException ex)
            {
                Debug.WriteLine(ex.Message);
                Directory.CreateDirectory(path);
                storageFolder = await StorageFolder.GetFolderFromPathAsync(path);
            }

            try
            {
                storageFile = await storageFolder.CreateFileAsync(filename, CreationCollisionOption.OpenIfExists);
            }
            catch (FileNotFoundException ex)
            {
                Debug.WriteLine(ex.Message);
                storageFile = await storageFolder.CreateFileAsync(filename);
            }

            try
            {
                if (jsonWindowPlacement != null)
                {
                    fileContents = jsonWindowPlacement.Stringify();
                    await Windows.Storage.FileIO.WriteTextAsync(storageFile, fileContents);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
