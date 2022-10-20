// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FileLocksmithUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ProcessEntry : UserControl
    {
        public uint Pid { get; private set; }

        public ProcessEntry(string process, uint pid, ulong numFiles)
        {
            Pid = pid;
            InitializeComponent();
            processName.Text = process;

            processPid.Text = PowerToys.FileLocksmithUI.Properties.Resources.ProcessId + ": " + pid;
            processFileCount.Text = PowerToys.FileLocksmithUI.Properties.Resources.FilesUsed + ": " + numFiles;
            processUser.Text = PowerToys.FileLocksmithUI.Properties.Resources.User + ": " +
                FileLocksmith.Interop.NativeMethods.PidToUser(pid);

            var icon = Icon.ExtractAssociatedIcon(FileLocksmith.Interop.NativeMethods.PidToFullPath(pid));
            if (icon != null)
            {
                Bitmap bitmap = icon.ToBitmap();
                BitmapImage bitmapImage = new BitmapImage();
                using (MemoryStream stream = new MemoryStream())
                {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
                    stream.Position = 0;
                    bitmapImage.SetSource(stream.AsRandomAccessStream());
                }

                processIcon.Source = bitmapImage;
            }
            else
            {
                // TODO put some default image
            }
        }

        public void AddPath(string path)
        {
            var entry = new TextBlock();
            entry.IsTextSelectionEnabled = true;
            entry.Text = path;
            entry.HorizontalAlignment = HorizontalAlignment.Left;

            filesContainer.Children.Add(entry);
        }

        private void KillProcessClick(object sender, RoutedEventArgs e)
        {
            if (!FileLocksmith.Interop.NativeMethods.KillProcess(Pid))
            {
                // TODO show something on failure.
            }
        }
    }
}
