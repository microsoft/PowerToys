// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal sealed partial class PerformanceSmallContent(WidgetPage widget, Size availableSpace) : IFormContent
{
    private readonly Lock _subscriptionLock = new();
    private TypedEventHandler<object, IPropChangedEventArgs>? _propChanged;

    public string DataJson => widget.GetSmallContentData().ToJsonString();

    public string StateJson => "{}";

    public string TemplateJson => $$"""
        {
          "$schema": "https://adaptivecards.io/schemas/adaptive-card.json",
          "type": "AdaptiveCard", "version": "1.5",
          "body": [
            { "type": "ColumnSet", "columns": [
              { "type": "Column", "width": "stretch", "items": [
                { "type": "TextBlock", "text": "${title}", "weight": "bolder" },
                { "type": "TextBlock", "text": "${value}",
                  "size": "{{(widget is SystemNetworkUsageWidgetPage or SystemDiskUsageWidgetPage ? "default" : "extraLarge")}}",
                  "weight": "bolder", "spacing": "none", "wrap": true, "maxLines": 2 },
                { "type": "TextBlock", "text": "${detail}", "size": "small", "isSubtle": true, "spacing": "none", "wrap": true, "maxLines": 2 },
                { "type": "TextBlock", "text": "${error}", "$when": "${error != ''}", "color": "attention", "wrap": true, "spacing": "none" }
              ] },
              { "type": "Column", "width": "auto", "$when": "${hasGraph}", "verticalContentAlignment": "center", "items": [
                { "type": "Image", "url": "${graph}", "height": "46px",
                  "width": "{{Math.Clamp((availableSpace.Width - 16) / 2, 60, 160).ToString(CultureInfo.InvariantCulture)}}px",
                  "altText": "${title}" }
              ] }
            ] }
          ]
        }
        """;

    public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
    {
        add
        {
            lock (_subscriptionLock)
            {
                var activate = _propChanged is null;
                _propChanged += value;
                if (activate)
                {
                    widget.Updated += OnUpdated;
                    widget.PushActivate();
                }
            }
        }

        remove
        {
            lock (_subscriptionLock)
            {
                var wasActive = _propChanged is not null;
                _propChanged -= value;
                if (wasActive && _propChanged is null)
                {
                    widget.Updated -= OnUpdated;
                    widget.PopActivate();
                }
            }
        }
    }

    public ICommandResult SubmitForm(string inputs, string data) => CommandResult.KeepOpen();

    private void OnUpdated(object? sender, EventArgs args)
    {
        TypedEventHandler<object, IPropChangedEventArgs>? handlers;
        lock (_subscriptionLock)
        {
            handlers = _propChanged;
        }

        EventHelpers.Raise(handlers, this, new PropChangedEventArgs(nameof(DataJson)), handler => PropChanged -= handler);
    }
}
