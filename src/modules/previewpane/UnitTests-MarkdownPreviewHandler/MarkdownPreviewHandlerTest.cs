// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using Microsoft.PowerToys.PreviewHandler.Markdown;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.WinForms;

namespace MarkdownPreviewHandlerUnitTests
{
    [STATestClass]
    public class MarkdownPreviewHandlerTest
    {
        private const int NavigateToStringUtf8LimitInBytes = 1_500_000;
        private const int OversizedUtf8FileThresholdInBytes = 2_000_000;

        // A long timeout is needed. WebView2 can take a long time to load the first time in some CI systems.
        private static readonly int HardTimeoutInMilliseconds = 60000;
        private static readonly int SleepTimeInMilliseconds = 200;

        [TestMethod]
        public void MarkdownPreviewHandlerControlAddsBrowserToFormWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
            {
                // Act
                markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/MarkdownWithExternalImage.txt");

                int beforeTick = Environment.TickCount;

                while (markdownPreviewHandlerControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.AreEqual(2, markdownPreviewHandlerControl.Controls.Count);
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[0], typeof(WebView2));
            }
        }

        [TestMethod]
        public void MarkdownPreviewHandlerControlAddsInfoBarToFormIfExternalImageLinkPresentWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
            {
                // Act
                markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/MarkdownWithExternalImage.txt");

                int beforeTick = Environment.TickCount;

                while (markdownPreviewHandlerControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.AreEqual(2, markdownPreviewHandlerControl.Controls.Count);
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[1], typeof(RichTextBox));
            }
        }

        [TestMethod]
        public void MarkdownPreviewHandlerControlAddsInfoBarToFormIfHTMLImageTagIsPresentWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
            {
                // Act
                markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/MarkdownWithHTMLImageTag.txt");

                int beforeTick = Environment.TickCount;

                while (markdownPreviewHandlerControl.Controls.Count < 2 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.AreEqual(2, markdownPreviewHandlerControl.Controls.Count);
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[1], typeof(RichTextBox));
            }
        }

        [TestMethod]
        public void MarkdownPreviewHandlerControlDoesNotAddInfoBarToFormIfExternalImageLinkNotPresentWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
            {
                // Act
                markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/MarkdownWithScript.txt");

                int beforeTick = Environment.TickCount;

                while (markdownPreviewHandlerControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.AreEqual(1, markdownPreviewHandlerControl.Controls.Count);
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[0], typeof(WebView2));
            }
        }

        [TestMethod]
        public void MarkdownPreviewHandlerControlUpdatesWebBrowserSettingsWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
            {
                // Act
                markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/MarkdownWithExternalImage.txt");

                int beforeTick = Environment.TickCount;

                while (markdownPreviewHandlerControl.Controls.Count < 2 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[0], typeof(WebView2));
                Assert.AreEqual(DockStyle.Fill, ((WebView2)markdownPreviewHandlerControl.Controls[0]).Dock);
            }
        }

        [TestMethod]
        public void MarkdownPreviewHandlerControlUpdateInfobarSettingsWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
            {
                // Act
                markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/MarkdownWithExternalImage.txt");

                int beforeTick = Environment.TickCount;

                while (markdownPreviewHandlerControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[1], typeof(RichTextBox));
                Assert.IsNotNull(((RichTextBox)markdownPreviewHandlerControl.Controls[1]).Text);
                Assert.AreEqual(DockStyle.Top, ((RichTextBox)markdownPreviewHandlerControl.Controls[1]).Dock);
                Assert.AreEqual(BorderStyle.None, ((RichTextBox)markdownPreviewHandlerControl.Controls[1]).BorderStyle);
                Assert.AreEqual(Color.LightYellow, ((RichTextBox)markdownPreviewHandlerControl.Controls[1]).BackColor);
                Assert.AreEqual(true, ((RichTextBox)markdownPreviewHandlerControl.Controls[1]).Multiline);
            }
        }

        [TestMethod]
        public void MarkdownPreviewHandlerControlUsesTempFileNavigationWhenUtf8ByteCountExceedsThresholdWithMultiByteCharacters()
        {
            string content = new string('漢', 700_000);

            Assert.IsTrue(content.Length < NavigateToStringUtf8LimitInBytes);
            Assert.IsTrue(Encoding.UTF8.GetByteCount(content) > OversizedUtf8FileThresholdInBytes);

            string filePath = CreateMarkdownFile(content);

            try
            {
                using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
                {
                    markdownPreviewHandlerControl.DoPreview(filePath);

                    WaitForBrowserControl(markdownPreviewHandlerControl);

                    AssertUsesTempFileNavigation(markdownPreviewHandlerControl);
                }
            }
            finally
            {
                DeleteFileIfExists(filePath);
            }
        }

        [TestMethod]
        public void MarkdownPreviewHandlerControlUsesNavigateToStringWhenContentIsWithinCharacterAndUtf8ByteThresholds()
        {
            string content = new string('a', 10_000);

            Assert.IsTrue(content.Length < NavigateToStringUtf8LimitInBytes);
            Assert.IsTrue(Encoding.UTF8.GetByteCount(content) < NavigateToStringUtf8LimitInBytes);

            string filePath = CreateMarkdownFile(content);

            try
            {
                using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
                {
                    markdownPreviewHandlerControl.DoPreview(filePath);

                    WaitForBrowserControl(markdownPreviewHandlerControl);

                    AssertUsesNavigateToString(markdownPreviewHandlerControl);
                }
            }
            finally
            {
                DeleteFileIfExists(filePath);
            }
        }

        [TestMethod]
        public void MarkdownPreviewHandlerControlUsesTempFileNavigationWhenAsciiContentExceedsCharacterThreshold()
        {
            string content = new string('a', 1_600_000);

            Assert.IsTrue(content.Length > NavigateToStringUtf8LimitInBytes);
            Assert.IsTrue(Encoding.UTF8.GetByteCount(content) > NavigateToStringUtf8LimitInBytes);

            string filePath = CreateMarkdownFile(content);

            try
            {
                using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
                {
                    markdownPreviewHandlerControl.DoPreview(filePath);

                    WaitForBrowserControl(markdownPreviewHandlerControl);

                    AssertUsesTempFileNavigation(markdownPreviewHandlerControl);
                }
            }
            finally
            {
                DeleteFileIfExists(filePath);
            }
        }

        private static void WaitForBrowserControl(MarkdownPreviewHandlerControl markdownPreviewHandlerControl)
        {
            int beforeTick = Environment.TickCount;

            while (markdownPreviewHandlerControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
            {
                Application.DoEvents();
                Thread.Sleep(SleepTimeInMilliseconds);
            }

            Assert.AreEqual(1, markdownPreviewHandlerControl.Controls.Count);
            Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[0], typeof(WebView2));
        }

        private static void AssertUsesTempFileNavigation(MarkdownPreviewHandlerControl markdownPreviewHandlerControl)
        {
            Uri localFileUri = GetLocalFileUri(markdownPreviewHandlerControl);

            Assert.IsNotNull(localFileUri);
            Assert.IsTrue(File.Exists(localFileUri.LocalPath));
            Assert.AreEqual(localFileUri, ((WebView2)markdownPreviewHandlerControl.Controls[0]).Source);
        }

        private static void AssertUsesNavigateToString(MarkdownPreviewHandlerControl markdownPreviewHandlerControl)
        {
            Assert.IsNull(GetLocalFileUri(markdownPreviewHandlerControl));
        }

        private static Uri GetLocalFileUri(MarkdownPreviewHandlerControl markdownPreviewHandlerControl)
        {
            FieldInfo localFileUriField = typeof(MarkdownPreviewHandlerControl).GetField("_localFileURI", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(localFileUriField);

            return (Uri)localFileUriField.GetValue(markdownPreviewHandlerControl);
        }

        private static string CreateMarkdownFile(string content)
        {
            string generatedFilesDirectory = Path.Combine(AppContext.BaseDirectory, "HelperFiles", "Generated");
            Directory.CreateDirectory(generatedFilesDirectory);

            string filePath = Path.Combine(generatedFilesDirectory, $"{Guid.NewGuid():N}.md");
            File.WriteAllText(filePath, content, Encoding.UTF8);
            return filePath;
        }

        private static void DeleteFileIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
