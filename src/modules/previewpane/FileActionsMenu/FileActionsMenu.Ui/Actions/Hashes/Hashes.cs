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
using System.Windows;
using Wpf.Ui.Controls;
using CheckedMenuItemsDictionairy = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<(Wpf.Ui.Controls.MenuItem, FileActionsMenu.Ui.Actions.IAction)>>;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace FileActionsMenu.Ui.Actions.Hashes.Hashes
{
    internal sealed class Hashes : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public string Header => "Generate Checksum";

        public IAction.ItemType Type => IAction.ItemType.HasSubMenu;

        public IAction[]? SubMenuItems =>
        [
            new SingleFile(),
            new MultipleFiles(),
            new InFilename(),
            new Separator(),
            new Md5(),
            new Sha1(),
            new Sha256(),
        ];

        public int Category => 0;

        public IconElement? Icon => new FontIcon { Glyph = "\uE73E" };

        public bool IsVisible => !SelectedItems.Any(Directory.Exists);

        public enum HashType
        {
            Md5,
            Sha1,
            Sha256,
        }

        public static Task GenerateHashes(HashType hashType, string[] selectedItems, CheckedMenuItemsDictionairy checkedMenuItemsDictionairy)
        {
            Func<string, string> hashGeneratorFunction;
            string fileExtension;

            switch (hashType)
            {
                case HashType.Md5:
#pragma warning disable CA5351
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return BitConverter.ToString(MD5.Create().ComputeHash(fs)).Replace("-", string.Empty);
                    };
#pragma warning restore CA5351
                    fileExtension = ".md5";
                    break;
                case HashType.Sha1:
#pragma warning disable CA5350
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return BitConverter.ToString(SHA1.Create().ComputeHash(fs)).Replace("-", string.Empty);
                    };
#pragma warning restore CA5350
                    fileExtension = ".sha1";
                    break;
                case HashType.Sha256:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return BitConverter.ToString(SHA256.Create().ComputeHash(fs)).Replace("-", string.Empty);
                    };
                    fileExtension = ".sha256";
                    break;
                default:
                    throw new InvalidOperationException("Unknown hash type");
            }

            List<(MenuItem, IAction)> checkedMenuItems = checkedMenuItemsDictionairy["2a89265d-a55a-4a48-b35f-a48f3e8bc2ea"];

            IAction checkedMenuItemAction = checkedMenuItems.First(checkedMenuItems => checkedMenuItems.Item1.IsChecked).Item2;

            if (checkedMenuItemAction is SingleFile)
            {
                GenerateSingleFileWithHashes(selectedItems, hashGeneratorFunction, fileExtension);
            }
            else if (checkedMenuItemAction is MultipleFiles)
            {
                GenerateMultipleFilesWithHashes(selectedItems, hashGeneratorFunction, fileExtension);
            }
            else if (checkedMenuItemAction is InFilename)
            {
                GenerateHashesInFilenames(selectedItems, hashGeneratorFunction);
            }
            else
            {
                throw new InvalidOperationException("Unknown checked menu item");
            }

            return Task.CompletedTask;
        }

        private static void GenerateHashesInFilenames(string[] selectedItems, Func<string, string> hashGeneratorFunction)
        {
            foreach (string filename in selectedItems)
            {
                string hash = hashGeneratorFunction(filename);

                File.Move(filename, Path.Combine(Path.GetDirectoryName(filename) ?? string.Empty, hash + Path.GetExtension(filename)));
            }
        }

        private static void GenerateSingleFileWithHashes(string[] selectedItems, Func<string, string> hashGeneratorFunction, string fileExtension)
        {
            StringBuilder fileContent = new();

            foreach (string filename in selectedItems)
            {
                fileContent.Append(filename + ":\n" + hashGeneratorFunction(filename) + "\n\n");
            }

            File.WriteAllText((Path.GetDirectoryName(selectedItems[0]) ?? throw new ArgumentNullException(nameof(selectedItems))) + "\\hashes" + fileExtension, fileContent.ToString());
        }

        private static void GenerateMultipleFilesWithHashes(string[] selectedItems, Func<string, string> hashGeneratorFunction, string fileExtension)
        {
            foreach (string filename in selectedItems)
            {
                string hash = hashGeneratorFunction(filename);

                string hashFilename = filename + fileExtension;

                File.WriteAllText(hashFilename, hash);
            }
        }

        public Task Execute(object sender, RoutedEventArgs e)
        {
            throw new InvalidOperationException("Inaccessible");
        }
    }
}
