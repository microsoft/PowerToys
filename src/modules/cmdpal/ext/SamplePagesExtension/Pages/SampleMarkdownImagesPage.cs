// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage;

namespace SamplePagesExtension.Pages;

internal sealed partial class SampleMarkdownImagesPage : ContentPage
{
    private static readonly Task InitializationTask;

    private static string? _sampleMarkdownText;

    static SampleMarkdownImagesPage()
    {
        InitializationTask = Task.Run(static async () =>
        {
            try
            {
                // prepare data files
                // 1) prepare something in our AppData Temp Folder
                var spaceFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Images/Space.png"));
                var tempFile = await spaceFile!.CopyAsync(ApplicationData.Current!.TemporaryFolder!, "Space.png", NameCollisionOption.ReplaceExisting);

                // 2) and also get an SVG directly from the package
                var svgChipmunkFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Images/FluentEmojiChipmunk.svg"));

                _sampleMarkdownText = GetContentMarkup(
                    new Uri(tempFile.Path!, UriKind.Absolute),
                    new Uri(svgChipmunkFile.Path!, UriKind.Absolute));
            }
            catch (Exception ex)
            {
                ExtensionHost.LogMessage(ex.ToString());
            }
        });
        return;

        static string GetContentMarkup(Uri path1, Uri path2)
        {
            return
          $$"""
            # Images in Markdown Content
            
            ## Available sources:

            - `![Alt Text](https://url)`

            - `![Alt Text](file://url)`
              - ℹ️ Only absolute paths are supported.
            
            - `![Alt Text](data:<mime>;[base64,]<data>)`
              - ⚠️ Only for small amount of data. Parsing large data blocks the UI.

            - `![Alt Text](ms-appx:///url)`
              - ⚠️ This loads from CmdPal's AppData folder, not Extension's, so it's not useful for extensions.

            - `![Alt Text](ms-appdata:///url)`
              - ⚠️ This loads from CmdPal's AppData folder, not Extension's, so it's not useful for extensions.

            ## Examples:

            ### Web URL
            ```xml
            ![painting](https://i.imgur.com/93XJSNh.png)
            ```
            ![painting](https://i.imgur.com/93XJSNh.png)
            
            ```xml
            ![painting](https://i.imgur.com/93XJSNh.png?--x-cmdpal-fit=fit)
            ```
            ![painting](https://i.imgur.com/93XJSNh.png?--x-cmdpal-fit=fit)

            ### File URL (PNG):
            ```xml
            ![green rectangle]({{path1}})
            ```

            ![green rectangle]({{path1}})
            
            ### File URL (SVG):
            ```xml
            ![chipmunk]({{path2}})
            ```
            
            ![chipmunk]({{path2}})
            
            ```xml
            ![chipmunk]({{path2}}?--x-cmdpal-maxwidth=400&--x-cmdpal-maxheight=400&--x-cmdpal-fit=fit)
            ```
            
            ![chipmunk]({{path2}}?--x-cmdpal-maxwidth=400&--x-cmdpal-maxheight=400&--x-cmdpal-fit=fit)
            
            ```xml
            ![chipmunk]({{path2}}?--x-cmdpal-width=64)
            ```
            ![chipmunk]({{path2}}?--x-cmdpal-width=64)

            ## Data URL (PNG):
            ⚠️ Passing large data into Markdown Content is unadvisable, parsing large data URLs can be slow and cause hangs.
            ```xml
            ![QR](data:image/png;base64,iVBORw0KGgoA...RU5ErkJggg==)
            ```

            ![QR](data:image/png;base64,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)

            ### Data URL (SVG):
            ⚠️ Passing large data into Markdown Content is unadvisable, parsing large data URLs can be slow and cause hangs.
            ```xml
            ![emoji](data:image/svg+xml;base64,PHN2ZyB4bWxucz0ia...NiAweiIvPjwvZz48L3N2Zz4=)
            ```
            
            ![emoji](data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIyNTYiIGhlaWdodD0iMjU2IiB2aWV3Qm94PSIwIDAgMzIgMzIiPjxnIGZpbGw9Im5vbmUiPjxwYXRoIGZpbGw9IiNGRkIwMkUiIGQ9Ik0xNS45OTkgMjkuOTk4YzkuMzM0IDAgMTMuOTk5LTYuMjY4IDEzLjk5OS0xNGMwLTcuNzMtNC42NjUtMTMuOTk4LTE0LTEzLjk5OEM2LjY2NSAyIDIgOC4yNjggMiAxNS45OTlzNC42NjQgMTMuOTk5IDEzLjk5OSAxMy45OTkiLz48cGF0aCBmaWxsPSIjZmZmIiBkPSJNMTAuNSAxOWE0LjUgNC41IDAgMSAwIDAtOWE0LjUgNC41IDAgMCAwIDAgOW0xMSAwYTQuNSA0LjUgMCAxIDAgMC05YTQuNSA0LjUgMCAwIDAgMCA5Ii8+PHBhdGggZmlsbD0iIzQwMkEzMiIgZD0iTTguMDcgNy45ODhjLS41OTQuNTYyLS45NTIgMS4yNC0xLjA5NiAxLjY3YS41LjUgMCAxIDEtLjk0OC0uMzE2Yy4xOS0uNTcuNjMtMS4zOTIgMS4zNTUtMi4wOEM4LjExMyA2LjU2NyA5LjE0OCA2IDEwLjUgNmEuNS41IDAgMCAxIDAgMWMtMS4wNDggMC0xLjg0Ni40MzMtMi40My45ODhNMTIgMTdhMiAyIDAgMSAwIDAtNGEyIDIgMCAwIDAgMCA0bTggMGEyIDIgMCAxIDAgMC00YTIgMiAwIDAgMCAwIDRtNS4wMjYtNy4zNDJjLS4xNDQtLjQzLS41MDMtMS4xMDgtMS4wOTUtMS42N0MyMy4zNDYgNy40MzMgMjIuNTQ4IDcgMjEuNSA3YS41LjUgMCAxIDEgMC0xYzEuMzUyIDAgMi4zODcuNTY3IDMuMTIgMS4yNjJjLjcyMy42ODggMS4xNjQgMS41MSAxLjM1NCAyLjA4YS41LjUgMCAxIDEtLjk0OC4zMTYiLz48cGF0aCBmaWxsPSIjQkIxRDgwIiBkPSJNMTMuMTcgMjJjLS4xMS4zMTMtLjE3LjY1LS4xNyAxdjJhMyAzIDAgMSAwIDYgMHYtMmMwLS4zNS0uMDYtLjY4Ny0uMTctMUwxNiAyMXoiLz48cGF0aCBmaWxsPSIjZmZmIiBkPSJNMTMuMTcgMjJhMy4wMDEgMy4wMDEgMCAwIDEgNS42NiAweiIvPjwvZz48L3N2Zz4=)
            
            ### Data URL (SVG 2):
            ⚠️ Passing large data into Markdown Content is unadvisable, parsing large data URLs can be slow and cause hangs.
            ```xml
            <img alt="emoji 2"
            width="48"
            height="48"
            src="data:image/svg+xml;base64,PHN2ZyB....iIvPjwvZz48L3N2Zz4=" />
            ```
            
            <img alt="emoji 2"
                 width="48"
                 height="48"
                 src="data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIyNTYiIGhlaWdodD0iMjU2IiB2aWV3Qm94PSIwIDAgMzIgMzIiPjxnIGZpbGw9Im5vbmUiPjxwYXRoIGZpbGw9IiNGRkIwMkUiIGQ9Ik0xNS45OTkgMjkuOTk4YzkuMzM0IDAgMTMuOTk5LTYuMjY4IDEzLjk5OS0xNGMwLTcuNzMtNC42NjUtMTMuOTk4LTE0LTEzLjk5OEM2LjY2NSAyIDIgOC4yNjggMiAxNS45OTlzNC42NjQgMTMuOTk5IDEzLjk5OSAxMy45OTkiLz48cGF0aCBmaWxsPSIjZmZmIiBkPSJNMTAuNSAxOWE0LjUgNC41IDAgMSAwIDAtOWE0LjUgNC41IDAgMCAwIDAgOW0xMSAwYTQuNSA0LjUgMCAxIDAgMC05YTQuNSA0LjUgMCAwIDAgMCA5Ii8+PHBhdGggZmlsbD0iIzQwMkEzMiIgZD0iTTguMDcgNy45ODhjLS41OTQuNTYyLS45NTIgMS4yNC0xLjA5NiAxLjY3YS41LjUgMCAxIDEtLjk0OC0uMzE2Yy4xOS0uNTcuNjMtMS4zOTIgMS4zNTUtMi4wOEM4LjExMyA2LjU2NyA5LjE0OCA2IDEwLjUgNmEuNS41IDAgMCAxIDAgMWMtMS4wNDggMC0xLjg0Ni40MzMtMi40My45ODhNMTIgMTdhMiAyIDAgMSAwIDAtNGEyIDIgMCAwIDAgMCA0bTggMGEyIDIgMCAxIDAgMC00YTIgMiAwIDAgMCAwIDRtNS4wMjYtNy4zNDJjLS4xNDQtLjQzLS41MDMtMS4xMDgtMS4wOTUtMS42N0MyMy4zNDYgNy40MzMgMjIuNTQ4IDcgMjEuNSA3YS41LjUgMCAxIDEgMC0xYzEuMzUyIDAgMi4zODcuNTY3IDMuMTIgMS4yNjJjLjcyMy42ODggMS4xNjQgMS41MSAxLjM1NCAyLjA4YS41LjUgMCAxIDEtLjk0OC4zMTYiLz48cGF0aCBmaWxsPSIjQkIxRDgwIiBkPSJNMTMuMTcgMjJjLS4xMS4zMTMtLjE3LjY1LS4xNyAxdjJhMyAzIDAgMSAwIDYgMHYtMmMwLS4zNS0uMDYtLjY4Ny0uMTctMUwxNiAyMXoiLz48cGF0aCBmaWxsPSIjZmZmIiBkPSJNMTMuMTcgMjJhMy4wMDEgMy4wMDEgMCAwIDEgNS42NiAweiIvPjwvZz48L3N2Zz4=" />
            
            ### MS-APPX URL:
            ⚠️ This loads from CmdPal's AppData folder, not Extension's, so it's not useful for extensions.
            ```xml
            ![Swirl](ms-appx:///Assets/Square44x44Logo.png)
            ```
            
            ![Swirl](ms-appx:///Assets/Square44x44Logo.png)
            
            ### MS-APPDATA URL:
            ⚠️ This loads from CmdPal's AppData folder, not Extension's, so it's not useful for extensions.
            ```xml
            ![Space](ms-appdata:///temp/Space.png)
            ```
            
            ---
            
            # Scaling
            
            For URIs that support query parameters (file, http, ms-appx, ms-appdata), you can provide hints to control scaling
            
            - `--x-cmdpal-fit`
              - `none`: no automatic scaling, provides image as is (default)
              - `fit`: scale to fit the available space
            - `--x-cmdpal-upscale`
              - `true`: allow upscaling
              - `false`: downscale only (default)
            - `--x-cmdpal-width`: desired width in pixels
            - `--x-cmdpal-height`: desired height in pixels
            - `--x-cmdpal-maxwidth`: max width in pixels
            - `--x-cmdpal-maxheight`: max height in pixels 
            
            Currently no support for data: scheme as it doesn't support query parameters at all.
            
            ## Examples
            
            ### No scaling
            ```xml
            ![green rectangle]({{path1}})
            ```
            
            ![green rectangle]({{path1}})
            
            ### Scale to fit (scaling down only by default)
            ```xml
            ![green rectangle]({{path1}}?--x-cmdpal-fit=fit)
            ```

            ![green rectangle]({{path1}}?--x-cmdpal-fit=fit)
            
            ### Scale to fit (in both direction)
            ```xml
            ![green rectangle]({{path1}}?--x-cmdpal-fit=fit&--x-cmdpal-upscale=true)
            ```
            
            ![green rectangle]({{path1}}?--x-cmdpal-fit=fit&--x-cmdpal-upscale=true)
            
            ### Scale to exact width
            ```xml
            ![green rectangle]({{path1}}?--x-cmdpal-width=320)
            ```
            
            ![green rectangle]({{path1}}?--x-cmdpal-width=320)
            """;
        }
    }

    private string _currentContent;

    public SampleMarkdownImagesPage()
    {
        Name = "Sample Markdown with Images Page";
        _currentContent = "Initializing...";
        IsLoading = true;

        _ = InitializationTask!.ContinueWith(_ =>
        {
            _currentContent = _sampleMarkdownText!;
            RaiseItemsChanged();
            IsLoading = false;
        });
    }

    public override IContent[] GetContent() => [new MarkdownContent(_currentContent ?? string.Empty)];
}
