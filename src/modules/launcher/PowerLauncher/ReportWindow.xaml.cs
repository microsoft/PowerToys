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
using Wox.Infrastructure.Logger;

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

            var paragraph = Hyperlink("Please open new issue in: ", Constant.Issue);
            paragraph.Inlines.Add($"1. upload log file: {log.FullName}\n");
            paragraph.Inlines.Add($"2. copy below exception message");
            ErrorTextbox.Document.Blocks.Add(paragraph);

            StringBuilder content = new StringBuilder();
            content.AppendLine(ErrorReporting.RuntimeInfo());
            content.AppendLine($"Date: {DateTime.Now.ToString(CultureInfo.InvariantCulture)}");
            content.AppendLine("Exception:");
            content.AppendLine(exception.ToString());
            paragraph = new Paragraph();
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

        private static void LinkOnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var ps = new ProcessStartInfo(e.Uri.ToString())
            {
                UseShellExecute = true,
                Verb = "open",
            };
            Process.Start(ps);
        }

        private static Paragraph Hyperlink(string textBeforeUrl, string url)
        {
            var paragraph = new Paragraph();
            paragraph.Margin = new Thickness(0);

            var link = new Hyperlink { IsEnabled = true };
            link.Inlines.Add(url);
            link.NavigateUri = new Uri(url);
            link.RequestNavigate += LinkOnRequestNavigate;

            paragraph.Inlines.Add(textBeforeUrl);
            paragraph.Inlines.Add(link);
            paragraph.Inlines.Add("\n");

            return paragraph;
        }
    }
}
