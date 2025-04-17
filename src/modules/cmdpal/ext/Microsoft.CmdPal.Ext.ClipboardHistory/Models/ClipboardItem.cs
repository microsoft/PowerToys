// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Ext.ClipboardHistory.Commands;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Models;

public class ClipboardItem
{
    public string? Content { get; set; }

    public required ClipboardHistoryItem Item { get; set; }

    public DateTimeOffset Timestamp => Item?.Timestamp ?? DateTimeOffset.MinValue;

    public RandomAccessStreamReference? ImageData { get; set; }

    public string GetDataType()
    {
        // Check if there is valid image data
        if (IsImage)
        {
            return "Image";
        }

        // Check if there is valid text content
        return IsText ? "Text" : "Unknown";
    }

    [MemberNotNullWhen(true, nameof(ImageData))]
    private bool IsImage => ImageData != null;

    [MemberNotNullWhen(true, nameof(Content))]
    private bool IsText => !string.IsNullOrEmpty(Content);

    public static List<string> ShiftLinesLeft(List<string> lines)
    {
        // Determine the minimum leading whitespace
        var minLeadingWhitespace = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Min(line => line.TakeWhile(char.IsWhiteSpace).Count());

        // Check if all lines have at least that much leading whitespace
        if (lines.Any(line => line.TakeWhile(char.IsWhiteSpace).Count() < minLeadingWhitespace))
        {
            return lines; // Return the original lines if any line doesn't have enough leading whitespace
        }

        // Remove the minimum leading whitespace from each line
        var shiftedLines = lines.Select(line => line.Substring(minLeadingWhitespace)).ToList();

        return shiftedLines;
    }

    public static List<string> StripLeadingWhitespace(List<string> lines)
    {
        // Determine the minimum leading whitespace
        var minLeadingWhitespace = lines
            .Min(line => line.TakeWhile(char.IsWhiteSpace).Count());

        // Remove the minimum leading whitespace from each line
        var shiftedLines = lines.Select(line =>
            line.Length >= minLeadingWhitespace
            ? line.Substring(minLeadingWhitespace)
            : line).ToList();

        return shiftedLines;
    }

    public ListItem ToListItem()
    {
        ListItem listItem;

        List<DetailsElement> metadata = [];
        metadata.Add(new DetailsElement()
        {
            Key = "Copied on",
            Data = new DetailsLink(Item.Timestamp.DateTime.ToString(DateTimeFormatInfo.CurrentInfo)),
        });

        if (IsImage)
        {
            var iconData = new IconData(ImageData);
            var heroImage = new IconInfo(iconData, iconData);
            listItem = new(new CopyCommand(this, ClipboardFormat.Image))
            {
                // Placeholder subtitle as there’s no BitmapImage dimensions to retrieve
                Title = "Image Data",
                Details = new Details()
                {
                    HeroImage = heroImage,
                    Title = GetDataType(),
                    Body = Timestamp.ToString(CultureInfo.InvariantCulture),
                    Metadata = metadata.ToArray(),
                },
                MoreCommands = [
                    new CommandContextItem(new PasteCommand(this, ClipboardFormat.Image))
                ],
            };
        }
        else if (IsText)
        {
            var splitContent = Content.Split("\n");
            var head = splitContent.AsSpan(0, Math.Min(3, splitContent.Length)).ToArray().ToList();
            var preview2 = string.Join(
                "\n",
                StripLeadingWhitespace(head));

            listItem = new(new CopyCommand(this, ClipboardFormat.Text))
            {
                Title = preview2,

                Details = new Details
                {
                    Title = GetDataType(),
                    Body = $"```text\n{Content}\n```",
                    Metadata = metadata.ToArray(),
                },
                MoreCommands = [
                                new CommandContextItem(new PasteCommand(this, ClipboardFormat.Text)),
                            ],
            };
        }
        else
        {
            listItem = new(new NoOpCommand())
            {
                Title = "Unknown",
                Subtitle = GetDataType(),
                Details = new Details { Title = GetDataType() },
            };
        }

        return listItem;
    }
}
