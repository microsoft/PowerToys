// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.ExtensionGallery.Models;

public sealed class GallerySourceDetails
{
    private const string SummaryLabel = "Summary";
    private const string DescriptionLabel = "Description";
    private const string VersionLabel = "Version";
    private const string TagsLabel = "Tags";

    public string? Summary { get; set; }

    public string? Description { get; set; }

    public string? Version { get; set; }

    public List<GallerySourceDetailItem> Items { get; set; } = [];

    public List<string> Tags { get; set; } = [];

    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool HasVersion => !string.IsNullOrWhiteSpace(Version);

    public bool HasItems => Items.Count > 0;

    public bool HasTags => Tags.Count > 0;

    public bool HasContent => HasSummary || HasDescription || HasVersion || HasItems || HasTags;

    public string TagsText => string.Join(", ", Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)));

    public List<GallerySourceDetailItem> FlattenedItems
    {
        get
        {
            List<GallerySourceDetailItem> flattened = [];

            AddFlattenedItem(flattened, SummaryLabel, Summary, null);
            AddFlattenedItem(flattened, DescriptionLabel, Description, null);
            AddFlattenedItem(flattened, VersionLabel, Version, null);

            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                AddFlattenedItem(flattened, item.Label, item.Value, item.LinkUri);
            }

            AddFlattenedItem(flattened, TagsLabel, TagsText, null);

            return flattened;
        }
    }

    private static void AddFlattenedItem(ICollection<GallerySourceDetailItem> target, string? label, string? value, Uri? linkUri)
    {
        var normalizedLabel = NormalizeToNullIfWhiteSpace(label);
        var normalizedValue = NormalizeToNullIfWhiteSpace(value);
        if (normalizedLabel is null || (normalizedValue is null && linkUri is null))
        {
            return;
        }

        target.Add(new GallerySourceDetailItem
        {
            Label = normalizedLabel,
            Value = normalizedValue ?? linkUri!.AbsoluteUri,
            LinkUri = linkUri,
        });
    }

    private static string? NormalizeToNullIfWhiteSpace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
