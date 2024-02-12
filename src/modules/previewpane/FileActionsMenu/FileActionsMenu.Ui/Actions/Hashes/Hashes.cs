// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;
using CheckedMenuItemsDictionairy = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<(Wpf.Ui.Controls.MenuItem, FileActionsMenu.Ui.Actions.IAction)>>;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace FileActionsMenu.Ui.Actions.Hashes.Hashes
{
    internal sealed class Hashes(Hashes.HashCallingAction hashCallingAction) : IAction
    {
        private HashCallingAction _hashCallingAction = hashCallingAction;

        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems ?? throw new ArgumentNullException(nameof(SelectedItems)); set => _selectedItems = value; }

        public string Header => _hashCallingAction == HashCallingAction.GENERATE ? "Generate checksum" : "Verify checksum" + ((SelectedItems.Length > 1) ? "s" : string.Empty);

        public IAction.ItemType Type => IAction.ItemType.HasSubMenu;

        public IAction[]? SubMenuItems =>
        [
            new SingleFile(_hashCallingAction),
            new MultipleFiles(_hashCallingAction),
            new InFilename(_hashCallingAction),
            new Separator(),
            new MD5(_hashCallingAction),
            new SHA1(_hashCallingAction),
            new SHA256(_hashCallingAction),
            new SHA384(_hashCallingAction),
            new SHA512(_hashCallingAction),
            new SHA3_256(_hashCallingAction),
            new SHA3_384(_hashCallingAction),
            new SHA3_512(_hashCallingAction),
            new CRC32(_hashCallingAction),
            new CRC64(_hashCallingAction),
        ];

        public int Category => 0;

        public IconElement? Icon => new FontIcon { Glyph = "\uE73E" };

        public bool IsVisible => !SelectedItems.Any(Directory.Exists);

        public enum HashType
        {
            MD5,
            SHA1,
            SHA256,
            SHA384,
            SHA512,
            SHA3_256,
            SHA3_384,
            SHA3_512,
            CRC32Hex,
            CRC32Decimal,
            CRC64Hex,
            CRC64Decimal,
            AUTHENTICODE,
        }

        public enum HashCallingAction
        {
            GENERATE,
            VERIFY,
        }

        public static Task VerifyHashes(HashType hashType, string[] selectedItems, CheckedMenuItemsDictionairy checkedMenuItemsDictionairy)
        {
            throw new NotImplementedException();
        }

        // Todo: Migrate to file action dialog
        public static Task GenerateHashes(HashType hashType, string[] selectedItems, CheckedMenuItemsDictionairy checkedMenuItemsDictionairy)
        {
            Func<string, string> hashGeneratorFunction;
            string fileExtension;

            switch (hashType)
            {
                case HashType.MD5:
#pragma warning disable CA5351
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return BitConverter.ToString(System.Security.Cryptography.MD5.Create().ComputeHash(fs)).Replace("-", string.Empty);
                    };
#pragma warning restore CA5351
                    fileExtension = ".md5";
                    break;
                case HashType.SHA1:
#pragma warning disable CA5350
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return BitConverter.ToString(System.Security.Cryptography.SHA1.Create().ComputeHash(fs)).Replace("-", string.Empty);
                    };
#pragma warning restore CA5350
                    fileExtension = ".sha1";
                    break;
                case HashType.SHA256:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(fs)).Replace("-", string.Empty);
                    };
                    fileExtension = ".sha256";
                    break;
                case HashType.SHA384:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return BitConverter.ToString(System.Security.Cryptography.SHA384.Create().ComputeHash(fs)).Replace("-", string.Empty);
                    };
                    fileExtension = ".sha384";
                    break;
                case HashType.SHA512:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return BitConverter.ToString(System.Security.Cryptography.SHA512.Create().ComputeHash(fs)).Replace("-", string.Empty);
                    };
                    fileExtension = ".sha512";
                    break;
                case HashType.SHA3_256:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return BitConverter.ToString(System.Security.Cryptography.SHA3_256.Create().ComputeHash(fs)).Replace("-", string.Empty);
                    };
                    fileExtension = ".sha3-256";
                    break;
                case HashType.SHA3_384:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return BitConverter.ToString(System.Security.Cryptography.SHA3_384.Create().ComputeHash(fs)).Replace("-", string.Empty);
                    };
                    fileExtension = ".sha3-384";
                    break;
                case HashType.SHA3_512:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return BitConverter.ToString(System.Security.Cryptography.SHA3_512.Create().ComputeHash(fs)).Replace("-", string.Empty);
                    };
                    fileExtension = ".sha3-512";
                    break;
                case HashType.CRC32Hex:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        Crc32 crc32 = new();
                        crc32.Append(fs);
                        return BitConverter.ToString(crc32.GetCurrentHash()).Replace("-", string.Empty);
                    };
                    fileExtension = ".crc32";
                    break;
                case HashType.CRC32Decimal:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        Crc32 crc32 = new();
                        crc32.Append(fs);
                        return crc32.GetCurrentHashAsUInt32().ToString(CultureInfo.InvariantCulture);
                    };
                    fileExtension = ".crc32";
                    break;
                case HashType.CRC64Hex:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        Crc64 crc64 = new();
                        crc64.Append(fs);
                        return BitConverter.ToString(crc64.GetCurrentHash()).Replace("-", string.Empty);
                    };
                    fileExtension = ".crc64";
                    break;
                case HashType.CRC64Decimal:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        Crc64 crc64 = new();
                        crc64.Append(fs);
                        return crc64.GetCurrentHashAsUInt64().ToString(CultureInfo.InvariantCulture);
                    };
                    fileExtension = ".crc64";
                    break;
                case HashType.AUTHENTICODE:
                    throw new NotImplementedException();
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
