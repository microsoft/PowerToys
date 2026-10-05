// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Widgets;
using Microsoft.Windows.Widgets.Providers;

namespace Microsoft.CmdPal.UI.Widgets;

[ComVisible(true)]
[Guid(ClassId)]
[ComDefaultInterface(typeof(IWidgetProvider))]
[WinRT.WinRTRuntimeClassName("Microsoft.CmdPal.UI.Widgets.CmdPalWidgetProvider")]
public sealed partial class CmdPalWidgetProvider : IWidgetProvider, IWidgetProvider2, IDisposable
{
    public const string ClassId = "918F58B7-7E1A-4E1A-A53E-6B0D760D83A3";

    private readonly WidgetCoordinator _coordinator;
    private readonly CancellationTokenSource _shutdown = new();
    private bool _disposed;

    public CmdPalWidgetProvider(WidgetCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public void CreateWidget(WidgetContext widgetContext)
    {
        var instanceId = widgetContext.Id;
        var size = widgetContext.Size.ToString();
        Run(ct => _coordinator.CreateAsync(instanceId, size, ct));
    }

    public void DeleteWidget(string widgetId, string customState) =>
        Run(ct => _coordinator.DeleteAsync(widgetId, customState, ct));

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        var instanceId = actionInvokedArgs.WidgetContext.Id;
        var verb = actionInvokedArgs.Verb ?? string.Empty;
        var data = actionInvokedArgs.Data;
        var customState = actionInvokedArgs.CustomState;
        Run(ct => _coordinator.ActionAsync(instanceId, verb, data, customState, ct));
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        var instanceId = contextChangedArgs.WidgetContext.Id;
        var size = contextChangedArgs.WidgetContext.Size.ToString();
        Run(ct => _coordinator.ContextChangedAsync(instanceId, size, ct));
    }

    public void Activate(WidgetContext widgetContext)
    {
        var instanceId = widgetContext.Id;
        var size = widgetContext.Size.ToString();
        Run(ct => _coordinator.ActivateAsync(instanceId, size, ct));
    }

    public void Deactivate(string widgetId) => Run(ct => _coordinator.DeactivateAsync(widgetId, ct));

    public void OnCustomizationRequested(WidgetCustomizationRequestedArgs customizationRequestedArgs)
    {
        var instanceId = customizationRequestedArgs.WidgetContext.Id;
        Logger.LogDebug($"Widget customization requested: {instanceId}");
        Run(ct => _coordinator.ShowCustomizationAsync(instanceId, ct));
    }

    private void Run(Func<CancellationToken, Task> operation)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await operation(_shutdown.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Logger.LogError("Widget provider operation failed", ex);
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        _shutdown.Dispose();
    }
}
