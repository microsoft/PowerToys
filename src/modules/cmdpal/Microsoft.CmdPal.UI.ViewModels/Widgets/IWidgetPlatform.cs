// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Widgets;

public interface IWidgetPlatform
{
    WidgetPlatformInfo? GetWidget(string widgetId);

    IReadOnlyList<WidgetPlatformInfo> GetWidgets();

    void Update(WidgetPlatformUpdate update);
}
