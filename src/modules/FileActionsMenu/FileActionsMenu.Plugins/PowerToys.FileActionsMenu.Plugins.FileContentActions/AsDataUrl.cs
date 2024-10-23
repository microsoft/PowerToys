// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Text.Json;
using System.Windows;
using FileActionsMenu.Helpers;
using FileActionsMenu.Helpers.Telemetry;
using FileActionsMenu.Interfaces;
using FileActionsMenu.Ui.Helpers;
using Microsoft.UI.Xaml.Controls;
using RoutedEventArgs = Microsoft.UI.Xaml.RoutedEventArgs;

namespace PowerToys.FileActionsMenu.Plugins.FileContentActions
{
    internal sealed class AsDataUrl : IAction
    {
        private string[]? _selectedItems;

        public string[] SelectedItems { get => _selectedItems.GetOrArgumentNullException(); set => _selectedItems = value; }

        public string Title => ResourceHelper.GetResource("File_Content_Actions.CopyContentAsDataUrl.Title");

        public IAction.ItemType Type => IAction.ItemType.SingleItem;

        public IAction[]? SubMenuItems => null;

        public int Category => 0;

        public IconElement? Icon => new FontIcon { Glyph = "\ue71b" };

        public bool IsVisible => SelectedItems.Length == 1 && !Directory.Exists(SelectedItems[0]);

        public Task Execute(object sender, RoutedEventArgs e)
        {
            string mimeType = GetMimeType(Path.GetExtension(SelectedItems[0]));
            byte[] fileContent = File.ReadAllBytes(SelectedItems[0]);
            string base64fileContent = Convert.ToBase64String(fileContent);
            System.Windows.Clipboard.SetText($"data:{mimeType};base64,{base64fileContent}");
            return Task.CompletedTask;
        }

        private string GetMimeType(string extension)
        {
            TelemetryHelper.LogEvent(new FileActionsMenuCopyContentAsDataUrlActionInvokedEvent(), SelectedItems);

            Dictionary<string, string> imageTypes = new()
            {
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png",
                [".gif"] = "image/gif",
                [".bmp"] = "image/bmp",
                [".svg"] = "image/svg+xml",
                [".heic"] = "image/heic",
                [".heif"] = "image/heif",
                [".svgz"] = "image/svg+xml",
                [".ico"] = "image/x-icon",
                [".cur"] = "image/x-icon",
                [".tif"] = "image/tiff",
                [".tiff"] = "image/tiff",
                [".webp"] = "image/webp",
                [".avif"] = "image/avif",
                [".apng"] = "image/apng",
                [".jxl"] = "image/jxl",
                [".jpe"] = "image/jpeg",
                [".jfif"] = "image/jpeg",
                [".pjpeg"] = "image/jpeg",
                [".pjp"] = "image",
            };

            if (imageTypes.TryGetValue(extension, out string? value))
            {
                return value;
            }

            JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(GetType())!.Location)!, "monaco_languages.json")));
            try
            {
                return jsonDocument.RootElement.GetProperty("list")
                    .EnumerateArray()
                    .First(predicate =>
                    {
                        return predicate.TryGetProperty("extensions", out JsonElement extensions) && extensions.EnumerateArray().Any(predicate => predicate.GetString() == extension);
                    })
                    .TryGetProperty("mimetypes", out JsonElement mimetypes)
                    ? mimetypes.EnumerateArray()
                    .FirstOrDefault()
                    .GetString() ?? "application/octet-stream"
                    : "application/octet-stream";
            }
            catch (InvalidOperationException)
            {
                return "application/octet-stream";
            }
        }
    }
}
