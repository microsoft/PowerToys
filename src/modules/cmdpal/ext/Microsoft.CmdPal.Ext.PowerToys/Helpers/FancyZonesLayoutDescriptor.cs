// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditorCommon.Data;

namespace PowerToysExtension.Helpers;

internal sealed class FancyZonesLayoutDescriptor
{
    public required string Id { get; init; } // "template:<type>" or "custom:<uuid>"

    public required FancyZonesLayoutSource Source { get; init; }

    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper ApplyLayout { get; init; }

    public LayoutTemplates.TemplateLayoutWrapper? Template { get; init; }

    public CustomLayouts.CustomLayoutWrapper? Custom { get; init; }
}
