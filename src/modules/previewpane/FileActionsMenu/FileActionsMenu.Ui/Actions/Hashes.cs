// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions
{
    internal static class Hashes
    {
        internal static void GenerateHashes(object sender, string[] selectedItems)
        {
            Func<string, string> hashGeneratorFunction;
#pragma warning disable CS0219 // Variable is assigned but its value is never used
            string fileExtension;
#pragma warning restore CS0219 // Variable is assigned but its value is never used

            switch (((System.Windows.Controls.MenuItem)sender).Name)
            {
                case "Md5HashMenuItem":
#pragma warning disable CA5351
                    hashGeneratorFunction = (string filename) => BitConverter.ToString(MD5.Create().ComputeHash(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))).Replace("-", string.Empty);
#pragma warning restore CA5351
                    fileExtension = ".md5";
                    break;
                case "Sha1HashMenuItem":
#pragma warning disable CA5350
                    hashGeneratorFunction = (string filename) => BitConverter.ToString(SHA1.Create().ComputeHash(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))).Replace("-", string.Empty);
#pragma warning restore CA5350
                    fileExtension = ".sha1";
                    break;
                case "Sha256HashMenuItem":
                    hashGeneratorFunction = (string filename) => BitConverter.ToString(SHA256.Create().ComputeHash(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))).Replace("-", string.Empty);
                    fileExtension = ".sha256";
                    break;
                default:
                    throw new InvalidOperationException("Unknown hash type");
            }

            FluentWindow window = new FluentWindow();
            window.Content = new ContentPresenter();
            ContentDialog contentDialog = new ContentDialog((ContentPresenter)window.Content);
            contentDialog.Title = "Save hashes to ... file(s)?";
            contentDialog.PrimaryButtonText = "Multiple";
            contentDialog.SecondaryButtonText = "Single";
            window.Width = 0;
            window.Height = 0;
            window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            try
            {
                window.Show();
            }
            catch (InvalidOperationException)
            {
                // Ignore
            }

            window.Focus();
            window.Activate();
            contentDialog.ShowAsync().ContinueWith((task) =>
            {
                if (task.Result == ContentDialogResult.Primary)
                {
                    foreach (string filename in selectedItems)
                    {
                        string hash = hashGeneratorFunction(filename);

                        string hashFilename = filename + fileExtension;

                        File.WriteAllText(hashFilename, hash);
                    }
                }
                else if (task.Result == ContentDialogResult.Secondary)
                {
                    StringBuilder fileContent = new();

                    foreach (string filename in selectedItems)
                    {
                        fileContent.Append(filename + ":\n" + hashGeneratorFunction(filename) + "\n\n");
                    }

                    File.WriteAllText((Path.GetDirectoryName(selectedItems[0]) ?? throw new ArgumentNullException(nameof(selectedItems))) + "\\hashes" + fileExtension, fileContent.ToString());
                }

                window.Dispatcher.Invoke(() => window.Close());
            });
        }
    }
}
