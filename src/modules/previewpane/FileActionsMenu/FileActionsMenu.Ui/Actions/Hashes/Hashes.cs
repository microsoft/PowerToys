// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace FileActionsMenu.Ui.Actions.Hashes.Hashes
{
    internal sealed class Hashes : IAction
    {
        public string[] SelectedItems { get => []; set => _ = value; }

        public string Header => "Generate Checksum";

        public bool HasSubMenu => true;

        public IAction[]? SubMenuItems =>
        [
            new Md5(),
            new Sha1(),
            new Sha256(),
        ];

        public int Category => 0;

        public IconElement? Icon => null;

        public bool IsVisible => true;

        public enum HashType
        {
            Md5,
            Sha1,
            Sha256,
        }

        public static async Task GenerateHashes(HashType hashType, string[] selectedItems)
        {
            Func<string, string> hashGeneratorFunction;
            string fileExtension;

            switch (hashType)
            {
                case HashType.Md5:
#pragma warning disable CA5351
                    hashGeneratorFunction = (filename) => BitConverter.ToString(MD5.Create().ComputeHash(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))).Replace("-", string.Empty);
#pragma warning restore CA5351
                    fileExtension = ".md5";
                    break;
                case HashType.Sha1:
#pragma warning disable CA5350
                    hashGeneratorFunction = (filename) => BitConverter.ToString(SHA1.Create().ComputeHash(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))).Replace("-", string.Empty);
#pragma warning restore CA5350
                    fileExtension = ".sha1";
                    break;
                case HashType.Sha256:
                    hashGeneratorFunction = (filename) => BitConverter.ToString(SHA256.Create().ComputeHash(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))).Replace("-", string.Empty);
                    fileExtension = ".sha256";
                    break;
                default:
                    throw new InvalidOperationException("Unknown hash type");
            }

            FluentWindow window = new();
            window.Content = new ContentPresenter();
            /*window.AllowsTransparency = true;
            window.Background = Brushes.Transparent;*/

            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(window);

            ContentDialog contentDialog = new((ContentPresenter)window.Content);
            contentDialog.Title = "Save hashes to ... file(s)?";
            contentDialog.PrimaryButtonText = "Multiple";
            contentDialog.SecondaryButtonText = "Single";
            window.Width = 0;
            window.Height = 0;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
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

            ManualResetEvent finishedEvent = new(false);

            ContentDialogResult result = await contentDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                window.Dispatcher.Invoke(window.Close);
                foreach (string filename in selectedItems)
                {
                    string hash = hashGeneratorFunction(filename);

                    string hashFilename = filename + fileExtension;

                    File.WriteAllText(hashFilename, hash);
                }
            }
            else if (result == ContentDialogResult.Secondary)
            {
                window.Dispatcher.Invoke(window.Close);
                StringBuilder fileContent = new();

                foreach (string filename in selectedItems)
                {
                    fileContent.Append(filename + ":\n" + hashGeneratorFunction(filename) + "\n\n");
                }

                File.WriteAllText((Path.GetDirectoryName(selectedItems[0]) ?? throw new ArgumentNullException(nameof(selectedItems))) + "\\hashes" + fileExtension, fileContent.ToString());
            }
        }

        public Task Execute(object sender, RoutedEventArgs e)
        {
            throw new InvalidOperationException("Inaccessible");
        }
    }
}
