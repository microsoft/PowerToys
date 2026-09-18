// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Widgets;

public sealed record WidgetPlatformUpdate(
    string WidgetId,
    string TemplateJson,
    string DataJson,
    string CustomState,
    bool IsPlaceholder = false);
