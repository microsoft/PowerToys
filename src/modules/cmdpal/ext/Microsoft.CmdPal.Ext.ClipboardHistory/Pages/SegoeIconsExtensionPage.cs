// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Pages;

#pragma warning disable SA1402 // File may only contain a single type

internal sealed partial class SegoeIconsExtensionPage : ListPage
{
    private readonly Lock _lock = new();

    private IListItem[]? _items;

    public SegoeIconsExtensionPage()
    {
        Icon = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory.ToString(), "Assets/WinUI3Gallery.png"));
        Name = "Segoe Icons";
        IsLoading = true;
        GridProperties = new SmallGridLayout();
        PreloadIcons();
    }

    public void PreloadIcons()
    {
        _ = Task.Run(() =>
        {
            lock (_lock)
            {
                var t = GenerateIconItems();
                t.ConfigureAwait(false);
                _items = t.Result;
            }
        });
    }

    public override IListItem[] GetItems()
    {
        lock (_lock)
        {
            IsLoading = false;
            return _items ?? [];
        }
    }

    private async Task<IListItem[]> GenerateIconItems()
    {
        var timer = new Stopwatch();
        timer.Start();
        var rawIcons = await IconsDataSource.Instance.LoadIcons()!;
        var items = rawIcons.Select(ToItem).ToArray();
        IsLoading = false;
        timer.Stop();
        ExtensionHost.LogMessage($"Generating icon items took {timer.ElapsedMilliseconds}ms");
        return items;
    }

    private IconListItem ToItem(IconData d) => new(d);
}

internal sealed partial class IconListItem : ListItem
{
    private readonly IconData _data;

    public IconListItem(IconData data)
        : base(new CopyTextCommand(data.CodeGlyph) { Name = $"Copy {data.CodeGlyph}" })
    {
        _data = data;
        this.Title = _data.Name;
        this.Icon = new IconInfo(data.Character);
        this.Subtitle = _data.CodeGlyph;
        if (data.Tags != null && data.Tags.Length > 0)
        {
            this.Tags = data.Tags.Select(t => new Tag() { Text = t }).ToArray();
        }

        this.MoreCommands =
        [
            new CommandContextItem(new CopyTextCommand(data.Character)) { Title = $"Copy {data.Character}", Icon = new IconInfo(data.Character) },
            new CommandContextItem(new CopyTextCommand(data.TextGlyph)) { Title = $"Copy {data.TextGlyph}" },
            new CommandContextItem(new CopyTextCommand(data.Name)) { Title = $"Copy {data.Name}" },
        ];
    }
}

// very shamelessly from
// https://github.com/microsoft/WinUI-Gallery/blob/main/WinUIGallery/DataModel/IconsDataSource.cs
public class IconData
{
    public required string Name { get; set; }

    public required string Code { get; set; }

    public string[] Tags { get; set; } = [];

    public string Character => char.ConvertFromUtf32(Convert.ToInt32(Code, 16));

    public string CodeGlyph => "\\u" + Code;

    public string TextGlyph => "&#x" + Code + ";";
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(List<IconData>), TypeInfoPropertyName = "IconList")]
internal sealed partial class IconDataListContext : JsonSerializerContext
{
}

internal sealed class IconsDataSource
{
    public static IconsDataSource Instance { get; } = new();

    public static List<IconData> Icons => Instance.icons;

    private List<IconData> icons = [];

    private IconsDataSource()
    {
    }

    private readonly object _lock = new();

    public async Task<List<IconData>> LoadIcons()
    {
        lock (_lock)
        {
            if (icons.Count != 0)
            {
                return icons;
            }
        }

        Stopwatch stopwatch = new();
        stopwatch.Start();
        var jsonText = await LoadText("Microsoft.CmdPal.Ext.ClipboardHistory/Assets/icons.json");
        lock (_lock)
        {
            if (icons.Count == 0 &&
                !string.IsNullOrEmpty(jsonText))
            {
                icons = JsonSerializer.Deserialize<List<IconData>>(jsonText, IconDataListContext.Default.IconList) is List<IconData> i ? i

                // icons = JsonSerializer.Deserialize<List<IconData>>(jsonText) is List<IconData> i ? i
                    : throw new InvalidDataException($"Cannot load icon data: {jsonText}");
            }

            stopwatch.Stop();
            ExtensionHost.LogMessage($"Reading file and parsing JSON took {stopwatch.ElapsedMilliseconds}ms");

            return icons;
        }
    }

    public static async Task<string> LoadText(string relativeFilePath)
    {
        // if the file exists, load it and append the new item
        var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory.ToString(), relativeFilePath);

        return File.Exists(sourcePath) ? await File.ReadAllTextAsync(sourcePath) : string.Empty;
    }
}
