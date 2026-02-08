// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Common;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Microsoft.PowerToys.PreviewHandler.Media
{
    /// <summary>
    /// Implementation of Control for Media Preview Handler using WebView2 with HTML5 video/audio player.
    /// </summary>
    public class MediaPreviewControl : FormHandlerControl
    {
        private const string VirtualHostName = "powertoys-media-preview";

        /// <summary>
        /// WebView2 Control to display media content.
        /// </summary>
        private WebView2 _webView2Control;

        /// <summary>
        /// Supported video file extensions.
        /// </summary>
        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".avi", ".mkv", ".mov", ".webm", ".wmv", ".m4v", ".3gp", ".3g2",
        };

        /// <summary>
        /// Supported audio file extensions.
        /// </summary>
        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma",
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaPreviewControl"/> class.
        /// </summary>
        public MediaPreviewControl()
        {
        }

        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="filePath">Path to the media file.</param>
        public void DoPreview(string filePath)
        {
            try
            {
                base.DoPreview(filePath);
                AddWebViewControl(filePath);
            }
            catch (Exception ex)
            {
                PreviewError(ex);
            }
        }

        /// <summary>
        /// Adds a WebView2 Control to display the media content.
        /// </summary>
        /// <param name="filePath">Path to the media file.</param>
        private void AddWebViewControl(string filePath)
        {
            _webView2Control = new WebView2
            {
                Dock = DockStyle.Fill,
            };

            _webView2Control.CoreWebView2InitializationCompleted += CoreWebView2_InitializationCompleted;

            Controls.Add(_webView2Control);
            ConfigureAsync(filePath);
        }

        /// <summary>
        /// Configures the WebView2 control asynchronously.
        /// </summary>
        /// <param name="filePath">Path to the media file.</param>
        private async void ConfigureAsync(string filePath)
        {
            try
            {
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "AppData",
                    "LocalLow",
                    "Microsoft",
                    "PowerToys",
                    "MediaPreviewHandler-Temp");

                var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
                await _webView2Control.EnsureCoreWebView2Async(environment);

                // Disable external navigation
                _webView2Control.CoreWebView2.Settings.IsScriptEnabled = true;
                _webView2Control.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView2Control.CoreWebView2.Settings.AreDevToolsEnabled = false;

                // Block navigation to external content
                _webView2Control.CoreWebView2.NavigationStarting += (s, e) =>
                {
                    // Only allow the initial page and local media mapped to the virtual host.
                    if (!e.Uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase) &&
                        !e.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) &&
                        !e.Uri.StartsWith($"https://{VirtualHostName}/", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                    }
                };

                var mediaUrl = MapMediaFileForWebView(filePath);
                var htmlContent = GenerateMediaHtml(filePath, mediaUrl);
                _webView2Control.CoreWebView2.NavigateToString(htmlContent);
            }
            catch (Exception ex)
            {
                PreviewError(ex);
            }
        }

        /// <summary>
        /// Maps the media file folder to a virtual host URL for WebView2 loading.
        /// </summary>
        /// <param name="filePath">Path to the media file.</param>
        /// <returns>Virtual-host URL pointing to the media file.</returns>
        private string MapMediaFileForWebView(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("Invalid media file path.", nameof(filePath));
            }

            _webView2Control.CoreWebView2.SetVirtualHostNameToFolderMapping(
                VirtualHostName,
                directory,
                CoreWebView2HostResourceAccessKind.Allow);

            return $"https://{VirtualHostName}/{Uri.EscapeDataString(fileName)}";
        }

        /// <summary>
        /// Handles WebView2 initialization completion.
        /// </summary>
        private void CoreWebView2_InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                PreviewError(e.InitializationException);
            }
        }

        /// <summary>
        /// Generates HTML content for playing media.
        /// </summary>
        /// <param name="filePath">Path to the media file.</param>
        /// <param name="mediaUrl">Media URL served through the WebView2 virtual host.</param>
        /// <returns>HTML string with embedded media player.</returns>
        private static string GenerateMediaHtml(string filePath, string mediaUrl)
        {
            var extension = Path.GetExtension(filePath);
            var isVideo = VideoExtensions.Contains(extension);
            var isAudio = AudioExtensions.Contains(extension);

            if (!isVideo && !isAudio)
            {
                throw new NotSupportedException($"Unsupported media format: {extension}");
            }

            // Determine MIME type
            var mimeType = GetMimeType(extension);

            var mediaElement = isVideo
                ? $@"<video id=""player"" controls style=""max-width: 100%; max-height: 100%; object-fit: contain;"">
                        <source src=""{mediaUrl}"" type=""{mimeType}"">
                        Your browser does not support the video tag.
                     </video>"
                : $@"<div class=""audio-container"">
                        <div class=""audio-icon"">🎵</div>
                        <audio id=""player"" controls style=""width: 100%;"">
                            <source src=""{mediaUrl}"" type=""{mimeType}"">
                            Your browser does not support the audio tag.
                        </audio>
                        <div class=""file-name"">{System.Net.WebUtility.HtmlEncode(Path.GetFileName(filePath))}</div>
                     </div>";

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            background-color: #1e1e1e;
            color: #ffffff;
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            overflow: hidden;
        }}
        video {{
            background-color: #000;
        }}
        .audio-container {{
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            gap: 20px;
            padding: 40px;
            text-align: center;
        }}
        .audio-icon {{
            font-size: 80px;
        }}
        .file-name {{
            font-size: 14px;
            color: #888;
            max-width: 300px;
            word-wrap: break-word;
        }}
        audio {{
            width: 300px;
        }}
        audio::-webkit-media-controls-panel {{
            background-color: #2d2d2d;
        }}
    </style>
</head>
<body>
    {mediaElement}
</body>
</html>";
        }

        /// <summary>
        /// Gets the MIME type for a file extension.
        /// </summary>
        /// <param name="extension">File extension.</param>
        /// <returns>MIME type string.</returns>
        private static string GetMimeType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                // Video types
                ".mp4" or ".m4v" => "video/mp4",
                ".webm" => "video/webm",
                ".avi" => "video/x-msvideo",
                ".mov" => "video/quicktime",
                ".mkv" => "video/x-matroska",
                ".wmv" => "video/x-ms-wmv",
                ".3gp" => "video/3gpp",
                ".3g2" => "video/3gpp2",

                // Audio types
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                ".flac" => "audio/flac",
                ".aac" => "audio/aac",
                ".m4a" => "audio/mp4",
                ".wma" => "audio/x-ms-wma",

                _ => "application/octet-stream",
            };
        }

        /// <summary>
        /// Called when an error occurs during preview.
        /// </summary>
        /// <param name="exception">The exception which occurred.</param>
        private void PreviewError(Exception exception)
        {
            Controls.Clear();
            var errorLabel = new Label
            {
                Text = $"Error loading media preview:\n{exception.Message}",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(30, 30, 30),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            Controls.Add(errorLabel);
        }
    }
}
