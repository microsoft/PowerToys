// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.VSCodeHelper
{
    public static class VSCodeInstances
    {
        private static readonly string _userAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public static List<VSCodeInstance> Instances { get; set; } = new List<VSCodeInstance>();

        private static BitmapImage Bitmap2BitmapImage(Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }

        public static Bitmap BitmapOverlayToCenter(Bitmap bitmap1, Bitmap overlayBitmap)
        {
            int bitmap1Width = bitmap1.Width;
            int bitmap1Height = bitmap1.Height;
            bitmap1.SetResolution(144, 144);
            using Bitmap overlayBitmapResized = new Bitmap(overlayBitmap, new System.Drawing.Size(bitmap1Width / 2, bitmap1Height / 2));

            float marginLeft = (float)((bitmap1Width * 0.7) - (overlayBitmapResized.Width * 0.5));
            float marginTop = (float)((bitmap1Height * 0.7) - (overlayBitmapResized.Height * 0.5));

            Bitmap finalBitmap = new Bitmap(bitmap1Width, bitmap1Height);
            using (Graphics g = Graphics.FromImage(finalBitmap))
            {
                g.DrawImage(bitmap1, System.Drawing.Point.Empty);
                g.DrawImage(overlayBitmapResized, marginLeft, marginTop);
            }

            return finalBitmap;
        }

        // Gets the executablePath and AppData foreach instance of VSCode
        public static void LoadVSCodeInstances()
        {
            var environmentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
            environmentPath += (environmentPath.Length > 0 && environmentPath.EndsWith(';') ? string.Empty : ";") + Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var paths = environmentPath
                .Split(';')
                .Distinct()
                .Where(x => x.Contains("VS Code", StringComparison.OrdinalIgnoreCase)
                    || x.Contains("VSCodium", StringComparison.OrdinalIgnoreCase)
                    || x.Contains("vscode", StringComparison.OrdinalIgnoreCase)).ToArray();

            foreach (var path in paths)
            {
                if (!Directory.Exists(path))
                {
                    continue;
                }

                var files = Directory.GetFiles(path)
                    .Where(x => (x.Contains("code", StringComparison.OrdinalIgnoreCase) || x.Contains("codium", StringComparison.OrdinalIgnoreCase))
                        && !x.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)).ToArray();

                // Remove the trailing backslash to always get the correct path
                var iconPath = Path.GetDirectoryName(path.TrimEnd('\\'));

                if (files.Length == 0)
                {
                    continue;
                }

                var file = files[0];
                var version = string.Empty;

                var instance = new VSCodeInstance
                {
                    ExecutablePath = file,
                };

                if (file.EndsWith("code", StringComparison.OrdinalIgnoreCase))
                {
                    version = "Code";
                    instance.VSCodeVersion = VSCodeVersion.Stable;
                }
                else if (file.EndsWith("code-insiders", StringComparison.OrdinalIgnoreCase))
                {
                    version = "Code - Insiders";
                    instance.VSCodeVersion = VSCodeVersion.Insiders;
                }
                else if (file.EndsWith("code-exploration", StringComparison.OrdinalIgnoreCase))
                {
                    version = "Code - Exploration";
                    instance.VSCodeVersion = VSCodeVersion.Exploration;
                }
                else if (file.EndsWith("codium", StringComparison.OrdinalIgnoreCase))
                {
                    version = "VSCodium";
                    instance.VSCodeVersion = VSCodeVersion.Stable;
                }
                else if (file.EndsWith("codium-insiders", StringComparison.OrdinalIgnoreCase))
                {
                    version = "VSCodium - Insiders";
                    instance.VSCodeVersion = VSCodeVersion.Insiders;
                }

                if (string.IsNullOrEmpty(version))
                {
                    continue;
                }

                var portableData = Path.Join(iconPath, "data");
                instance.AppData = Directory.Exists(portableData) ? Path.Join(portableData, "user-data") : Path.Combine(_userAppDataPath, version);
                var vsCodeIconPath = Path.Join(iconPath, $"{version}.exe");
                if (!File.Exists(vsCodeIconPath))
                {
                    continue;
                }

                var vsCodeIcon = Icon.ExtractAssociatedIcon(vsCodeIconPath);

                if (vsCodeIcon == null)
                {
                    continue;
                }

                using var vsCodeIconBitmap = vsCodeIcon.ToBitmap();

                // Workspace
                using var folderIcon = (Bitmap)Image.FromFile(Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Images//folder.png"));
                using var bitmapFolderIcon = BitmapOverlayToCenter(folderIcon, vsCodeIconBitmap);
                instance.WorkspaceIconBitMap = Bitmap2BitmapImage(bitmapFolderIcon);

                // Remote
                using var monitorIcon = (Bitmap)Image.FromFile(Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Images//monitor.png"));
                using var bitmapMonitorIcon = BitmapOverlayToCenter(monitorIcon, vsCodeIconBitmap);
                instance.RemoteIconBitMap = Bitmap2BitmapImage(bitmapMonitorIcon);

                Instances.Add(instance);
            }
        }
    }
}
