// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.IO.Hashing;
using System.Text;
using System.Windows;
using FileActionsMenu.Helpers;
using FileActionsMenu.Helpers.Telemetry;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;
using static FileActionsMenu.Helpers.HashEnums;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;

namespace PowerToys.FileActionsMenu.Plugins.Hashes
{
    public sealed class Hashes(Hashes.HashCallingAction hashCallingAction) : IAction
    {
        private readonly HashCallingAction _hashCallingAction = hashCallingAction;

        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => _hashCallingAction == HashCallingAction.GENERATE ? (SelectedItems.Length == 1 ? ResourceHelper.GetResource("Hashes.Generate.Title_S") : ResourceHelper.GetResource("Hashes.Generate.Title_P")) : (SelectedItems.Length == 1 ? ResourceHelper.GetResource("Hashes.Verify.Title_S") : ResourceHelper.GetResource("Hashes.Verify.Title_P"));

        public IAction.ItemType Type => IAction.ItemType.HasSubMenu;

        public IAction[]? SubMenuItems =>
        [
            new SingleFile(_hashCallingAction),
            new MultipleFiles(_hashCallingAction),
            new InFilename(_hashCallingAction),
            new InClipboard(_hashCallingAction),
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

        public IconElement? Icon => _hashCallingAction == HashCallingAction.GENERATE ? new FontIcon { Glyph = "\uE73A" } : new FontIcon { Glyph = "\uE9D5" };

        public bool IsVisible => !SelectedItems.Any(Directory.Exists);

        public enum HashCallingAction
        {
            GENERATE,
            VERIFY,
        }

        private static (Func<string, string> HashGeneratorFunction, string FileExtension) GetHashProperties(HashType hashType)
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
                        return Convert.ToHexString(System.Security.Cryptography.MD5.Create().ComputeHash(fs));
                    };
#pragma warning restore CA5351
                    fileExtension = ".md5";
                    break;
                case HashType.SHA1:
#pragma warning disable CA5350
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return Convert.ToHexString(System.Security.Cryptography.SHA1.Create().ComputeHash(fs));
                    };
#pragma warning restore CA5350
                    fileExtension = ".sha1";
                    break;
                case HashType.SHA256:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return Convert.ToHexString(System.Security.Cryptography.SHA256.Create().ComputeHash(fs));
                    };
                    fileExtension = ".sha256";
                    break;
                case HashType.SHA384:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return Convert.ToHexString(System.Security.Cryptography.SHA384.Create().ComputeHash(fs));
                    };
                    fileExtension = ".sha384";
                    break;
                case HashType.SHA512:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return Convert.ToHexString(System.Security.Cryptography.SHA512.Create().ComputeHash(fs));
                    };
                    fileExtension = ".sha512";
                    break;
                case HashType.SHA3_256:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return Convert.ToHexString(System.Security.Cryptography.SHA3_256.Create().ComputeHash(fs));
                    };
                    fileExtension = ".sha3-256";
                    break;
                case HashType.SHA3_384:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return Convert.ToHexString(System.Security.Cryptography.SHA3_384.Create().ComputeHash(fs));
                    };
                    fileExtension = ".sha3-384";
                    break;
                case HashType.SHA3_512:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        return Convert.ToHexString(System.Security.Cryptography.SHA3_512.Create().ComputeHash(fs));
                    };
                    fileExtension = ".sha3-512";
                    break;
                case HashType.CRC32Hex:
                    hashGeneratorFunction = (filename) =>
                    {
                        using FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        Crc32 crc32 = new();
                        crc32.Append(fs);
                        return Convert.ToHexString(crc32.GetCurrentHash());
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
                        return Convert.ToHexString(crc64.GetCurrentHash());
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
                default:
                    throw new InvalidOperationException("Unknown hash type");
            }

            return (hashGeneratorFunction, fileExtension);
        }

        public static Task VerifyHashes(HashType hashType, string[] selectedItems, CheckedMenuItemsDictionary checkedMenuItemsDictionary)
        {
            (Func<string, string> hashGeneratorFunction, string fileExtension) = GetHashProperties(hashType);
            List<(MenuFlyoutItemBase, IAction)> checkedMenuItems = checkedMenuItemsDictionary[GetUUID(HashCallingAction.VERIFY)];

            IAction checkedMenuItemAction = checkedMenuItems.First(checkedMenuItems => ((ToggleMenuFlyoutItem)checkedMenuItems.Item1).IsChecked).Item2;

            bool valid = checkedMenuItemAction switch
            {
                SingleFile => VerifySingleFileWithHashes(selectedItems, hashGeneratorFunction, fileExtension),
                MultipleFiles => VerifyMultipleFilesWithHashes(selectedItems, hashGeneratorFunction, fileExtension),
                InFilename => VerifyHashesInFilenames(selectedItems, hashGeneratorFunction),
                InClipboard => Clipboard.GetText() == hashGeneratorFunction(selectedItems[0]),
                _ => throw new InvalidOperationException("Unknown checked menu item"),
            };

            GenerateOrVerifyMode verifyMode = checkedMenuItemAction switch
            {
                SingleFile => GenerateOrVerifyMode.SingleFile,
                MultipleFiles => GenerateOrVerifyMode.MultipleFiles,
                InFilename => GenerateOrVerifyMode.Filename,
                InClipboard => GenerateOrVerifyMode.Clipboard,
                _ => throw new InvalidOperationException("Unknown checked menu item"),
            };

            TelemetryHelper.LogEvent(new FileActionsMenuVerifyHashesActionInvokedEvent() { HashType = hashType, VerifyMode = verifyMode }, selectedItems);

            if (valid)
            {
                MessageBox.Show(ResourceHelper.GetResource("Hashes.Verify.Dialog.Success"), ResourceHelper.GetResource("Hashes.Verify.Dialog.Title"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(ResourceHelper.GetResource("Hashes.Verify.Dialog.Fail"), ResourceHelper.GetResource("Hashes.Verify.Dialog.Title"), MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return Task.CompletedTask;
        }

        public static async Task GenerateHashes(HashType hashType, string[] selectedItems, CheckedMenuItemsDictionary checkedMenuItemsDictionary)
        {
            (Func<string, string> hashGeneratorFunction, string fileExtension) = GetHashProperties(hashType);
            List<(MenuFlyoutItemBase, IAction)> checkedMenuItems = checkedMenuItemsDictionary[GetUUID(HashCallingAction.GENERATE)];

            IAction checkedMenuItemAction = checkedMenuItems.First(checkedMenuItems => ((ToggleMenuFlyoutItem)checkedMenuItems.Item1).IsChecked).Item2;

            switch (checkedMenuItemAction)
            {
                case SingleFile:
                    await GenerateSingleFileWithHashes(selectedItems, hashGeneratorFunction, fileExtension);
                    break;
                case MultipleFiles:
                    GenerateMultipleFilesWithHashes(selectedItems, hashGeneratorFunction, fileExtension);
                    break;
                case InFilename:
                    GenerateHashesInFilenames(selectedItems, hashGeneratorFunction);
                    break;
                case InClipboard:
                    Clipboard.SetText(hashGeneratorFunction(selectedItems[0]));
                    break;
                default:
                    throw new InvalidOperationException("Unknown checked menu item");
            }

            GenerateOrVerifyMode generateMode = checkedMenuItemAction switch
            {
                SingleFile => GenerateOrVerifyMode.SingleFile,
                MultipleFiles => GenerateOrVerifyMode.MultipleFiles,
                InFilename => GenerateOrVerifyMode.Filename,
                InClipboard => GenerateOrVerifyMode.Clipboard,
                _ => throw new InvalidOperationException("Unknown checked menu item"),
            };

            TelemetryHelper.LogEvent(new FileActionsMenuGenerateHashesActionInvokedEvent() { HashType = hashType, GenerateMode = generateMode }, selectedItems);
        }

        private static void GenerateHashesInFilenames(string[] selectedItems, Func<string, string> hashGeneratorFunction)
        {
            foreach (string filename in selectedItems)
            {
                string hash = hashGeneratorFunction(filename);

                File.Move(filename, Path.Combine(Path.GetDirectoryName(filename) ?? string.Empty, hash + Path.GetExtension(filename)), true);
            }
        }

        private static bool VerifyHashesInFilenames(string[] selectedItems, Func<string, string> hashGeneratorFunction)
        {
            foreach (string filename in selectedItems)
            {
                string hash = hashGeneratorFunction(filename);

                return Path.GetFileNameWithoutExtension(filename) == hash;
            }

            throw new InvalidOperationException();
        }

        private static async Task GenerateSingleFileWithHashes(string[] selectedItems, Func<string, string> hashGeneratorFunction, string fileExtension)
        {
            FileActionProgressHelper fileActionProgressHelper = new(ResourceHelper.GetResource("Hashes.Generate.Dialog.Title"), 1, () => { });
            fileActionProgressHelper.UpdateProgress(0, ResourceHelper.GetResource("Hashes.Generate.Filename") + fileExtension);

            StringBuilder fileContent = new();

            foreach (string filename in selectedItems)
            {
                fileContent.Append(filename + ":\n" + hashGeneratorFunction(filename) + "\n\n");
            }

            if (File.Exists(Path.GetDirectoryName(selectedItems[0]).GetOrArgumentNullException() + "\\" + ResourceHelper.GetResource("Hashes.Generate.Filename") + fileExtension))
            {
                await fileActionProgressHelper.Conflict(Path.GetDirectoryName(selectedItems[0]).GetOrArgumentNullException() + "\\Checksums" + fileExtension, () => File.WriteAllText(Path.GetDirectoryName(selectedItems[0]).GetOrArgumentNullException() + "\\" + ResourceHelper.GetResource("Hashes.Generate.Filename") + fileExtension, fileContent.ToString()), () => { });
            }
            else
            {
                File.WriteAllText(Path.GetDirectoryName(selectedItems[0]).GetOrArgumentNullException() + "\\" + ResourceHelper.GetResource("Hashes.Generate.Filename") + fileExtension, fileContent.ToString());
            }
        }

        private static bool VerifySingleFileWithHashes(string[] selectedItems, Func<string, string> hashGeneratorFunction, string fileExtension)
        {
            string checksumsFilename = Path.GetDirectoryName(selectedItems[0]).GetOrArgumentNullException() + "\\" + ResourceHelper.GetResource("Hashes.Generate.Filename") + fileExtension;

            if (!File.Exists(checksumsFilename))
            {
#pragma warning disable CA1863 // Use "CompositeFormat"
                MessageBox.Show(string.Format(CultureInfo.InvariantCulture, ResourceHelper.GetResource("Hashes.Verify.Dialog.MissingFile"), checksumsFilename), ResourceHelper.GetResource("Hashes.Verify.Dialog.Title"), MessageBoxButton.OK);
#pragma warning restore CA1863 // Use "CompositeFormat"

                return false;
            }

            string[] checksums = File.ReadAllLines(checksumsFilename);

            foreach (string filename in selectedItems)
            {
                string hash = hashGeneratorFunction(filename);

                if (!checksums.Contains(filename + ":"))
                {
                    return false;
                }

                if (checksums[Array.IndexOf(checksums, filename + ":") + 1] != hash)
                {
                    return false;
                }
            }

            return true;
        }

        private static void GenerateMultipleFilesWithHashes(string[] selectedItems, Func<string, string> hashGeneratorFunction, string fileExtension)
        {
            foreach (string filename in selectedItems)
            {
                string hash = hashGeneratorFunction(filename);

                string hashFilename = filename + fileExtension;

                if (File.Exists(hashFilename))
                {
                    File.Delete(hashFilename);
                }

                File.WriteAllText(hashFilename, hash);
            }
        }

        private static bool VerifyMultipleFilesWithHashes(string[] selectedItems, Func<string, string> hashGeneratorFunction, string fileExtension)
        {
            foreach (string filename in selectedItems)
            {
                string hash = hashGeneratorFunction(filename);

                string hashFilename = filename + fileExtension;

                if (!File.Exists(hashFilename))
                {
#pragma warning disable CA1863 // Use "CompositeFormat"
                    MessageBox.Show(string.Format(CultureInfo.InvariantCulture, ResourceHelper.GetResource("Hashes.Verify.Dialog.MissingFile"), hashFilename), ResourceHelper.GetResource("Hashes.Verify.Dialog.Title"), MessageBoxButton.OK);
#pragma warning restore CA1863 // Use "CompositeFormat"

                    return false;
                }

                if (File.ReadAllText(hashFilename) != hash)
                {
                    return false;
                }
            }

            return true;
        }

        public static string GetUUID(HashCallingAction hashCallingAction)
        {
            return hashCallingAction == HashCallingAction.GENERATE ? "2a89265d-a55a-4a48-b35f-a48f3e8bc2ea" : "2a89265d-a55a-4a48-b35f-a48f3e8bc2eb";
        }

        public Task Execute(object sender, RoutedEventArgs e)
        {
            throw new InvalidOperationException("Inaccessible");
        }
    }
}
