// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed record WidgetDefinition(
    string ExtensionId,
    string ProviderId,
    string WidgetId,
    string Title,
    string Description,
    string SourceName,
    string IconText,
    IIconInfo Icon,
    WidgetSize[] SupportedSizes,
    bool AllowMultiple);
