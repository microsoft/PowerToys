// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Widgets;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Svg;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI;
using Microsoft.Windows.Widgets.Providers;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Widgets;

public sealed class WindowsWidgetPlatform : IWidgetPlatform
{
    private const int IconSize = 32;
    private const int MaxIconBytes = 128 * 1024;
    private readonly ConditionalWeakTable<IIconInfo, Lazy<Task<string?>>> _icons = new();
    private readonly Lazy<string?> _defaultIcon = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "StoreLogo.png");
        return File.Exists(path) ? "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(path)) : null;
    });

    public WidgetPlatformInfo? GetWidget(string widgetId)
    {
        var info = WidgetManager.GetDefault().GetWidgetInfo(widgetId);
        return info is null ? null : new(info.WidgetContext.Id, info.CustomState ?? string.Empty);
    }

    public IReadOnlyList<WidgetPlatformInfo> GetWidgets() => WidgetManager.GetDefault()
        .GetWidgetInfos()
        .Select(info => new WidgetPlatformInfo(info.WidgetContext.Id, info.CustomState ?? string.Empty))
        .ToArray();

    public void Update(WidgetPlatformUpdate update)
    {
        string? iconUrl = null;
        try
        {
            if (update.HeaderIcon is not null)
            {
                var iconTask = _icons.GetValue(update.HeaderIcon, icon => new(() => Task.Run(() => RenderIconAsync(icon)))).Value;
                iconUrl = iconTask.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load widget header icon", ex);
        }

        try
        {
            iconUrl ??= _defaultIcon.Value;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load the default widget header icon", ex);
        }

        var title = string.IsNullOrWhiteSpace(update.HeaderTitle) ? "Command Palette" : update.HeaderTitle;
        var options = new WidgetUpdateRequestOptions(update.WidgetId)
        {
            Template = update.TemplateWithHeader(title, iconUrl),
            Data = update.DataJson,
            CustomState = update.CustomState,
            IsPlaceholderContent = update.IsPlaceholder,
        };
        WidgetManager.GetDefault().UpdateWidget(options);
    }

    internal static async Task<string?> RenderIconAsync(IIconInfo icon)
    {
        try
        {
            var source = icon.Light ?? icon.Dark;
            var model = new IconDataViewModel(source);
            model.InitializeProperties();
            var value = model.Icon;
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            {
                return value;
            }

            if (value.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) && value.Length <= MaxIconBytes)
            {
                return value;
            }

            var device = CanvasDevice.GetSharedDevice();
            using var target = new CanvasRenderTarget(device, IconSize, IconSize, 96);
            using (var drawing = target.CreateDrawingSession())
            {
                drawing.Clear(Colors.Transparent);
                if (!string.IsNullOrEmpty(value) && value.Length <= 8 && !value.Contains('\\') && !value.Contains('/'))
                {
                    var fontFamily = model.FontFamily;
                    if (string.IsNullOrWhiteSpace(fontFamily))
                    {
                        fontFamily = value.Any(character => character >= '\uE000' && character <= '\uF8FF')
                            ? "Segoe Fluent Icons"
                            : "Segoe UI Emoji";
                    }

                    using var format = new CanvasTextFormat
                    {
                        FontFamily = fontFamily,
                        FontSize = 22,
                        HorizontalAlignment = CanvasHorizontalAlignment.Center,
                        VerticalAlignment = CanvasVerticalAlignment.Center,
                    };
                    drawing.FillRoundedRectangle(0, 0, IconSize, IconSize, 6, 6, Colors.RoyalBlue);
                    drawing.DrawText(value, new Rect(0, 0, IconSize, IconSize), Colors.White, format);
                }
                else
                {
                    using var stream = await OpenIconStreamAsync(value, model.Data.Unsafe).ConfigureAwait(false);
                    if (stream is null || stream.Size > MaxIconBytes)
                    {
                        return null;
                    }

                    if (value.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    {
                        using var svg = await CanvasSvgDocument.LoadAsync(device, stream);
                        drawing.DrawSvg(svg, new Size(IconSize, IconSize));
                    }
                    else
                    {
                        using var bitmap = await CanvasBitmap.LoadAsync(device, stream);
                        drawing.DrawImage(bitmap, new Rect(0, 0, IconSize, IconSize));
                    }
                }
            }

            using var output = new InMemoryRandomAccessStream();
            await target.SaveAsync(output, CanvasBitmapFileFormat.Png);
            output.Seek(0);
            using var reader = new DataReader(output);
            await reader.LoadAsync((uint)output.Size);
            var bytes = new byte[(int)output.Size];
            reader.ReadBytes(bytes);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError("Could not convert extension icon for the Widgets Board", ex);
            return null;
        }
    }

    private static async Task<IRandomAccessStream?> OpenIconStreamAsync(string value, IRandomAccessStreamReference? streamReference)
    {
        if (string.IsNullOrEmpty(value))
        {
            return streamReference is null ? null : await streamReference.OpenReadAsync();
        }

        StorageFile file;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "ms-appx")
        {
            file = await StorageFile.GetFileFromApplicationUriAsync(uri);
        }
        else
        {
            var path = uri?.IsFile == true ? uri.LocalPath : value;
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path) || new FileInfo(path).Length > MaxIconBytes)
            {
                return null;
            }

            file = await StorageFile.GetFileFromPathAsync(path);
        }

        return await file.OpenReadAsync();
    }
}
