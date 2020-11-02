// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using PowerLauncher.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.Image;
using Wox.Plugin.Logger;

namespace PowerLauncher
{
    internal partial class ReportWindow
    {
        public ReportWindow(Exception exception)
        {
            InitializeComponent();
            BitmapImage image = GetImageFromPath(ImageLoader.ErrorIconPath);
            if (image != null)
            {
                Icon = image;
            }

            ErrorTextbox.Document.Blocks.FirstBlock.Margin = new Thickness(0);
            SetException(exception);
        }

        private void SetException(Exception exception)
        {
            string path = Log.CurrentLogDirectory;
            var directory = new DirectoryInfo(path);
            var log = directory.GetFiles().OrderByDescending(f => f.LastWriteTime).First();

            LogFilePathBox.Text = log.FullName;

            StringBuilder content = new StringBuilder();
            content.AppendLine(ErrorReporting.RuntimeInfo());

            // Using CurrentCulture since this is displayed to user in the report window
            content.AppendLine($"Date: {DateTime.Now.ToString(CultureInfo.CurrentCulture)}");
            content.AppendLine("Exception:");
            content.AppendLine(exception.ToString());
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(content.ToString());
            ErrorTextbox.Document.Blocks.Add(paragraph);
        }

        // Function to get the Bitmap Image from the path
        private static BitmapImage GetImageFromPath(string path)
        {
            if (File.Exists(path))
            {
                MemoryStream memoryStream = new MemoryStream();

                byte[] fileBytes = File.ReadAllBytes(path);
                memoryStream.Write(fileBytes, 0, fileBytes.Length);
                memoryStream.Position = 0;

                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = memoryStream;
                image.EndInit();
                return image;
            }
            else
            {
                return null;
            }
        }

        private void RepositoryHyperlink_Click(object sender, RoutedEventArgs e)
        {
            var ps = new ProcessStartInfo((sender as Hyperlink).NavigateUri.ToString())
            {
                UseShellExecute = true,
                Verb = "open",
            };
            Process.Start(ps);
        }
    }
}
