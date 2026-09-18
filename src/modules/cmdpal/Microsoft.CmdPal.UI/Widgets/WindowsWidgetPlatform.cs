// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Widgets;
using Microsoft.Windows.Widgets.Providers;

namespace Microsoft.CmdPal.UI.Widgets;

public sealed class WindowsWidgetPlatform : IWidgetPlatform
{
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
        var options = new WidgetUpdateRequestOptions(update.WidgetId)
        {
            Template = update.TemplateJson,
            Data = update.DataJson,
            CustomState = update.CustomState,
            IsPlaceholderContent = update.IsPlaceholder,
        };
        WidgetManager.GetDefault().UpdateWidget(options);
    }
}
