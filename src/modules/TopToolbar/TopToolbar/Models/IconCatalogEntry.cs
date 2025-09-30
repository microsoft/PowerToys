// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace TopToolbar.Models
{
    public sealed class IconCatalogEntry
    {
        public IconCatalogEntry(
            string id,
            string displayName,
            string category,
            Uri resourceUri = null,
            IReadOnlyList<string> keywords = null,
            string description = "",
            string glyph = "",
            string fontFamily = "")
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentNullException(nameof(displayName));
            }

            Id = id;
            DisplayName = displayName;
            Category = category ?? string.Empty;
            ResourceUri = resourceUri;
            Glyph = glyph?.Trim() ?? string.Empty;
            FontFamily = fontFamily?.Trim() ?? string.Empty;
            Keywords = keywords ?? Array.Empty<string>();
            Description = description ?? string.Empty;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public string Category { get; }

        public Uri ResourceUri { get; }

        public string Glyph { get; }

        public string FontFamily { get; }

        public IReadOnlyList<string> Keywords { get; }

        public string Description { get; }

        public bool HasGlyph => !string.IsNullOrWhiteSpace(Glyph);

        public bool HasImage => ResourceUri != null;
    }
}
