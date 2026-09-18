// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

public partial class SamplePagesCommandsProvider : CommandProvider
{
    private readonly SampleWidgetContent _widgetDefinition = new(string.Empty);

    public SamplePagesCommandsProvider()
    {
        DisplayName = "Sample Pages Commands";
        Icon = new IconInfo("\uE82D");
    }

    private readonly ICommandItem[] _commands = [
       new CommandItem(new SamplesListPage())
       {
           Title = "Sample Pages",
           Subtitle = "View example commands",
       },
    ];

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    public override ICommandItem[] GetDockBands()
    {
        List<ICommandItem> bands = new()
        {
            new SampleDockBand(),
            new SampleButtonsDockBand(),
        };

        return bands.ToArray();
    }

    public override IWidgetContent[] GetWidgets() => [_widgetDefinition];

    public override IWidgetContent GetWidget(string id, string instanceId) =>
        id == SampleWidgetContent.WidgetId ? new SampleWidgetContent(instanceId) : null;
}
