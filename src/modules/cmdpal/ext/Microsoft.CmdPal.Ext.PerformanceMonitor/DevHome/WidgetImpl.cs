// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Windows.Widgets.Providers;

// using Serilog;
namespace CoreWidgetProvider.Widgets;

public abstract class WidgetImpl
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private const int SHORT_ID_LENGTH = 6;
#pragma warning restore SA1310 // Field names should not contain underscore

    private string _state = string.Empty;

    public WidgetImpl()
    {
        // _log = new(() => Serilog.Log.ForContext("SourceContext", SourceName));
    }

    // private readonly Lazy<ILogger> _log;

    // protected ILogger Log => _log.Value;
    protected string Name => GetType().Name;

    protected string Id { get; set; } = string.Empty;

    // This is not a unique identifier, but is easier to read in a log and highly unlikely to
    // match another running widget.
    protected string ShortId => Id.Length > SHORT_ID_LENGTH ? Id[..SHORT_ID_LENGTH] : Id;

    protected string SourceName => string.IsNullOrEmpty(ShortId) ? Name : $"{Name}/{ShortId}";

    public string State()
    {
        return _state;
    }

    public void SetState(string state)
    {
        _state = state;
    }

    public abstract void CreateWidget(WidgetContext widgetContext, string state);

    public abstract void Activate(WidgetContext widgetContext);

    public abstract void Deactivate(string widgetId);

    public abstract void DeleteWidget(string widgetId, string customState);

    public abstract void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs);

    public abstract void OnCustomizationRequested(WidgetCustomizationRequestedArgs customizationRequestedArgs);

    public abstract void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs);
}
