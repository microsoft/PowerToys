// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Static helpers that materialize JSON-RPC payloads into the Command Palette
/// toolkit data types (icons, tags, details, content, grid layouts, filters).
/// Keeping the JSON key literals inside these helpers (rather than inside
/// properties named after the keys) keeps the adapters analyzer clean.
/// </summary>
internal static class JSModelMapper
{
    internal static string? GetString(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    internal static bool GetBool(JsonElement element, string name, bool defaultValue)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.False)
            {
                return false;
            }
        }

        return defaultValue;
    }

    internal static bool TryGetAnyCase(JsonElement element, string camel, string pascal, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(camel, out value))
            {
                return true;
            }

            if (element.TryGetProperty(pascal, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    internal static IIconInfo GetIcon(JsonElement parent, string camel, string pascal)
    {
        if (TryGetAnyCase(parent, camel, pascal, out var iconProp))
        {
            return ParseIconInfo(iconProp);
        }

        return new IconInfo(string.Empty);
    }

    internal static IIconInfo ParseIconInfo(JsonElement iconJson)
    {
        if (iconJson.ValueKind == JsonValueKind.String)
        {
            return new IconInfo(iconJson.GetString());
        }

        if (iconJson.ValueKind != JsonValueKind.Object)
        {
            return new IconInfo(string.Empty);
        }

        IconData? light = null;
        IconData? dark = null;

        if (TryGetAnyCase(iconJson, "light", "Light", out var lightProp))
        {
            light = ParseIconData(lightProp);
        }

        if (TryGetAnyCase(iconJson, "dark", "Dark", out var darkProp))
        {
            dark = ParseIconData(darkProp);
        }

        if (light == null && dark == null)
        {
            if (TryGetAnyCase(iconJson, "icon", "Icon", out var singleIcon) ||
                TryGetAnyCase(iconJson, "data", "Data", out singleIcon))
            {
                var shared = ParseIconData(iconJson);
                return new IconInfo(shared, shared);
            }

            return new IconInfo(string.Empty);
        }

        light ??= new IconData(string.Empty);
        dark ??= new IconData(string.Empty);
        return new IconInfo(light, dark);
    }

    internal static IconData ParseIconData(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return MakeIconData(element.GetString(), null);
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return new IconData(string.Empty);
        }

        string? iconString = null;
        string? dataString = null;

        if (TryGetAnyCase(element, "icon", "Icon", out var iconProp) && iconProp.ValueKind == JsonValueKind.String)
        {
            iconString = iconProp.GetString();
        }

        if (TryGetAnyCase(element, "data", "Data", out var dataProp) && dataProp.ValueKind == JsonValueKind.String)
        {
            dataString = dataProp.GetString();
        }

        return MakeIconData(iconString, dataString);
    }

    internal static ITag[] ParseTags(JsonElement parent)
    {
        if (!TryGetAnyCase(parent, "tags", "Tags", out var tagsProp) || tagsProp.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var tags = new List<ITag>();
        foreach (var tagElement in tagsProp.EnumerateArray())
        {
            tags.Add(ParseTag(tagElement));
        }

        return tags.ToArray();
    }

    internal static ITag ParseTag(JsonElement element)
    {
        var tag = new Tag
        {
            Text = GetString(element, "text") ?? GetString(element, "Text") ?? string.Empty,
            ToolTip = GetString(element, "toolTip") ?? GetString(element, "tooltip") ?? GetString(element, "ToolTip") ?? string.Empty,
            Icon = GetIcon(element, "icon", "Icon"),
            Foreground = ParseColor(element, "foreground", "Foreground"),
            Background = ParseColor(element, "background", "Background"),
        };

        return tag;
    }

    internal static OptionalColor ParseColor(JsonElement parent, string camel, string pascal)
    {
        if (!TryGetAnyCase(parent, camel, pascal, out var colorProp) || colorProp.ValueKind != JsonValueKind.Object)
        {
            return ColorHelpers.NoColor();
        }

        var container = colorProp;
        if (TryGetAnyCase(colorProp, "hasValue", "HasValue", out var hasValueProp))
        {
            if (hasValueProp.ValueKind == JsonValueKind.False)
            {
                return ColorHelpers.NoColor();
            }

            if (TryGetAnyCase(colorProp, "color", "Color", out var inner) && inner.ValueKind == JsonValueKind.Object)
            {
                container = inner;
            }
        }

        byte r = ReadByte(container, "r", "R");
        byte g = ReadByte(container, "g", "G");
        byte b = ReadByte(container, "b", "B");
        byte a = ReadByte(container, "a", "A", 255);
        return ColorHelpers.FromArgb(a, r, g, b);
    }

    internal static IDetails? ParseDetails(JsonElement parent, JsonRpcConnection? connection)
    {
        if (!TryGetAnyCase(parent, "details", "Details", out var detailsProp) || detailsProp.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new Details
        {
            Title = GetString(detailsProp, "title") ?? GetString(detailsProp, "Title") ?? string.Empty,
            Body = GetString(detailsProp, "body") ?? GetString(detailsProp, "Body") ?? string.Empty,
            HeroImage = GetIcon(detailsProp, "heroImage", "HeroImage"),
            Metadata = ParseMetadata(detailsProp, connection),
        };
    }

    internal static IContextItem[] ParseContextItems(JsonElement parent, string camel, string pascal, JsonRpcConnection connection)
    {
        if (!TryGetAnyCase(parent, camel, pascal, out var arrayProp) || arrayProp.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<IContextItem>();
        foreach (var element in arrayProp.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (GetBool(element, "_isSeparator", false))
            {
                items.Add(new Separator(GetString(element, "title") ?? string.Empty));
                continue;
            }

            items.Add(ParseContextItem(element, connection));
        }

        return items.ToArray();
    }

    internal static ICommandContextItem ParseContextItem(JsonElement element, JsonRpcConnection connection)
    {
        var commandData = element;
        if (TryGetAnyCase(element, "command", "Command", out var commandElement) && commandElement.ValueKind == JsonValueKind.Object)
        {
            commandData = commandElement;
        }

        var command = JSCommandFactory.CreateCommandFromJson(commandData, connection);
        var item = new CommandContextItem(command)
        {
            Icon = GetIcon(element, "icon", "Icon"),
            IsCritical = GetBool(element, "isCritical", false),
        };

        var title = GetString(element, "title") ?? GetString(element, "Title");
        if (!string.IsNullOrEmpty(title))
        {
            item.Title = title;
        }

        var subtitle = GetString(element, "subtitle") ?? GetString(element, "Subtitle");
        if (!string.IsNullOrEmpty(subtitle))
        {
            item.Subtitle = subtitle;
        }

        return item;
    }

    internal static IGridProperties? ParseGridProperties(JsonElement parent)
    {
        if (!TryGetAnyCase(parent, "gridProperties", "GridProperties", out var gridProp) || gridProp.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var layout = GetString(gridProp, "layout") ?? string.Empty;
        var showTitle = GetBool(gridProp, "showTitle", true);
        var showSubtitle = GetBool(gridProp, "showSubtitle", true);

        return layout switch
        {
            "small" => new SmallGridLayout(),
            "medium" => new MediumGridLayout { ShowTitle = showTitle },
            "gallery" => new GalleryGridLayout { ShowTitle = showTitle, ShowSubtitle = showSubtitle },
            _ => null,
        };
    }

    internal static IFilterItem[] ParseFilterItems(JsonElement parent)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty("filters", out var filtersProp) ||
            filtersProp.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var filters = new List<IFilterItem>();
        foreach (var element in filtersProp.EnumerateArray())
        {
            if (GetBool(element, "_isSeparator", false))
            {
                filters.Add(new Separator(GetString(element, "title") ?? string.Empty));
                continue;
            }

            filters.Add(new Filter
            {
                Id = GetString(element, "id") ?? string.Empty,
                Name = GetString(element, "name") ?? string.Empty,
                Icon = GetIcon(element, "icon", "Icon"),
            });
        }

        return filters.ToArray();
    }

    internal static IContent[] ParseContentArray(JsonElement? result, string pageId, JsonRpcConnection connection)
    {
        if (!result.HasValue || result.Value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var count = result.Value.GetArrayLength();
        var items = new IContent[count];
        var i = 0;
        foreach (var element in result.Value.EnumerateArray())
        {
            items[i++] = ParseContentItem(element, pageId, connection);
        }

        return items;
    }

    internal static IContent ParseContentItem(JsonElement element, string pageId, JsonRpcConnection connection)
    {
        var type = GetString(element, "type") ?? string.Empty;

        switch (type)
        {
            case "form":
                return new JSFormContentProxy(pageId, element, connection);

            case "image":
                return new ImageContent(GetIcon(element, "image", "Image"))
                {
                    MaxWidth = ReadInt(element, "maxWidth", -1),
                    MaxHeight = ReadInt(element, "maxHeight", -1),
                };

            case "plainText":
                return new PlainTextContent(GetString(element, "text") ?? string.Empty)
                {
                    FontFamily = GetString(element, "fontFamily") == "monospace" ? FontFamily.Monospace : FontFamily.UserInterface,
                    WrapWords = GetBool(element, "wrapWords", true),
                };

            case "tree":
                return ParseTreeContent(element, pageId, connection);

            default:
                return new MarkdownContent(GetString(element, "body") ?? string.Empty);
        }
    }

    private static IContent ParseTreeContent(JsonElement element, string pageId, JsonRpcConnection connection)
    {
        var tree = new TreeContent();

        if (TryGetAnyCase(element, "rootContent", "RootContent", out var rootProp) && rootProp.ValueKind == JsonValueKind.Object)
        {
            tree.RootContent = ParseContentItem(rootProp, pageId, connection);
        }

        if (TryGetAnyCase(element, "children", "Children", out var childrenProp) && childrenProp.ValueKind == JsonValueKind.Array)
        {
            var count = childrenProp.GetArrayLength();
            var children = new IContent[count];
            var i = 0;
            foreach (var child in childrenProp.EnumerateArray())
            {
                children[i++] = ParseContentItem(child, pageId, connection);
            }

            tree.Children = children;
        }

        return tree;
    }

    private static IDetailsElement[] ParseMetadata(JsonElement details, JsonRpcConnection? connection)
    {
        if (!TryGetAnyCase(details, "metadata", "Metadata", out var metaProp) || metaProp.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var elements = new List<IDetailsElement>();
        foreach (var element in metaProp.EnumerateArray())
        {
            var key = GetString(element, "key") ?? GetString(element, "Key") ?? string.Empty;
            IDetailsData? data = null;
            if (TryGetAnyCase(element, "data", "Data", out var dataProp))
            {
                data = ParseDetailsData(dataProp, connection);
            }

            elements.Add(new DetailsElement { Key = key, Data = data });
        }

        return elements.ToArray();
    }

    private static IDetailsData? ParseDetailsData(JsonElement element, JsonRpcConnection? connection)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var type = GetString(element, "type") ?? string.Empty;

        switch (type)
        {
            case "separator":
                return new DetailsSeparator();

            case "link":
                var link = new DetailsLink
                {
                    Text = GetString(element, "text") ?? string.Empty,
                };
                var linkStr = GetString(element, "link");
                if (!string.IsNullOrEmpty(linkStr) && Uri.TryCreate(linkStr, UriKind.Absolute, out var uri))
                {
                    link.Link = uri;
                }

                return link;

            case "tags":
                return new DetailsTags { Tags = ParseTags(element) };

            case "commands":
                if (connection != null && element.TryGetProperty("commands", out var cmdsProp) && cmdsProp.ValueKind == JsonValueKind.Array)
                {
                    var commands = new List<ICommand>();
                    foreach (var cmdEl in cmdsProp.EnumerateArray())
                    {
                        if (cmdEl.ValueKind == JsonValueKind.Object)
                        {
                            commands.Add(JSCommandFactory.CreateCommandFromJson(cmdEl, connection));
                        }
                    }

                    return new DetailsCommands { Commands = commands.ToArray() };
                }

                return null;

            default:
                if (TryGetAnyCase(element, "tags", "Tags", out var fallbackTags) && fallbackTags.ValueKind == JsonValueKind.Array)
                {
                    return new DetailsTags { Tags = ParseTags(element) };
                }

                return null;
        }
    }

    private static IconData MakeIconData(string? iconString, string? dataString)
    {
        if (!string.IsNullOrEmpty(dataString))
        {
            var streamReference = DecodeToStreamReference(dataString);
            if (streamReference != null)
            {
                return new IconData(streamReference);
            }
        }

        return new IconData(iconString ?? string.Empty);
    }

    private static IRandomAccessStreamReference? DecodeToStreamReference(string dataString)
    {
        try
        {
            byte[] bytes = dataString.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? DecodeDataUri(dataString)
                : Convert.FromBase64String(dataString);

            var stream = new InMemoryRandomAccessStream();
            stream.WriteAsync(bytes.AsBuffer()).GetResults();
            stream.Seek(0);
            return RandomAccessStreamReference.CreateFromStream(stream);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to decode icon data: {ex.Message}");
            return null;
        }
    }

    private static byte[] DecodeDataUri(string dataUri)
    {
        var commaIndex = dataUri.IndexOf(',', StringComparison.Ordinal);
        if (commaIndex < 0)
        {
            throw new FormatException("Invalid data URI: no comma separator.");
        }

        var header = dataUri[..commaIndex];
        var payload = dataUri[(commaIndex + 1)..];

        if (header.Contains(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.FromBase64String(payload);
        }

        return Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
    }

    private static byte ReadByte(JsonElement element, string camel, string pascal, byte defaultValue = 0)
    {
        if (TryGetAnyCase(element, camel, pascal, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetByte();
        }

        return defaultValue;
    }

    private static int ReadInt(JsonElement element, string name, int defaultValue)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetInt32();
        }

        return defaultValue;
    }
}
