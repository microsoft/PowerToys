// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Microsoft.PowerToys.PreviewHandler.Media
{
    /// <summary>
    /// Implementation of Control for Media Preview Handler using WebView2 with HTML5 video/audio player.
    /// </summary>
    public class MediaPreviewControl : FormHandlerControl
    {
        /// <summary>
        /// WebView2 Control to display media content.
        /// </summary>
        private WebView2? _webView2Control;

        /// <summary>
        /// File path of the media file to preview.
        /// </summary>
        private string? _filePath;

        /// <summary>
        /// Supported video file extensions.
        /// </summary>
        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".3g2", ".3gp", ".3gp2", ".3gpp", ".asf", ".avi", ".m2t", ".m2ts",
            ".m4v", ".mkv", ".mov", ".mp4v", ".mts", ".wm", ".wmv", ".webm",
        };

        /// <summary>
        /// Supported audio file extensions.
        /// </summary>
        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".aac", ".ac3", ".amr", ".flac", ".m4a", ".mp3", ".ogg", ".wav", ".wma",
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
                _filePath = filePath;
                InvokeOnControlThread(() =>
                {
                    try
                    {
                        AddWebViewControl(filePath);
                    }
                    catch (Exception ex)
                    {
                        PreviewError(ex);
                    }
                });
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
                var userDataFolder = Environment.GetEnvironmentVariable("USERPROFILE") +
                                     "\\AppData\\LocalLow\\Microsoft\\PowerToys\\MediaPreviewHandler-Temp";

                var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
                await _webView2Control!.EnsureCoreWebView2Async(environment);

                // Disable external navigation
                _webView2Control.CoreWebView2.Settings.IsScriptEnabled = true;
                _webView2Control.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView2Control.CoreWebView2.Settings.AreDevToolsEnabled = false;

                // Generate and navigate to HTML content
                var htmlContent = GenerateMediaHtml(filePath);
                _webView2Control.CoreWebView2.NavigateToString(htmlContent);
            }
            catch (Exception ex)
            {
                PreviewError(ex);
            }
        }

        /// <summary>
        /// Handles WebView2 initialization completion.
        /// </summary>
        private void CoreWebView2_InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
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
        /// <returns>HTML string with embedded media player.</returns>
        private static string GenerateMediaHtml(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            var isVideo = VideoExtensions.Contains(extension);
            var isAudio = AudioExtensions.Contains(extension);

            // Convert file path to file:// URL
            var fileUrl = new Uri(filePath).AbsoluteUri;

            // Determine MIME type
            var mimeType = GetMimeType(extension);

            var mediaElement = isVideo
                ? $@"<video id=""player"" controls autoplay style=""max-width: 100%; max-height: 100%; object-fit: contain;"">
                        <source src=""{fileUrl}"" type=""{mimeType}"">
                        Your browser does not support the video tag.
                     </video>"
                : $@"<div class=""audio-container"">
                        <div class=""audio-icon"">🎵</div>
                        <audio id=""player"" controls autoplay style=""width: 100%;"">
                            <source src=""{fileUrl}"" type=""{mimeType}"">
                            Your browser does not support the audio tag.
                        </audio>
                        <div class=""file-name"">{Path.GetFileName(filePath)}</div>
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
                ".mp4" or ".m4v" or ".mp4v" => "video/mp4",
                ".webm" => "video/webm",
                ".avi" => "video/x-msvideo",
                ".mov" => "video/quicktime",
                ".mkv" => "video/x-matroska",
                ".wmv" or ".wm" => "video/x-ms-wmv",
                ".3gp" or ".3gpp" or ".3g2" or ".3gp2" => "video/3gpp",
                ".m2ts" or ".mts" or ".m2t" => "video/mp2t",
                ".asf" => "video/x-ms-asf",

                // Audio types
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                ".flac" => "audio/flac",
                ".aac" => "audio/aac",
                ".m4a" => "audio/mp4",
                ".wma" => "audio/x-ms-wma",
                ".ac3" => "audio/ac3",
                ".amr" => "audio/amr",

                _ => "application/octet-stream",
            };
        }

        /// <summary>
        /// Called when an error occurs during preview.
        /// </summary>
        /// <param name="exception">The exception which occurred.</param>
        private void PreviewError(Exception exception)
        {
            InvokeOnControlThread(() =>
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
            });
        }
    }
}
