// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;

namespace Microsoft.CmdPal.UI.ViewModels;

public abstract partial class ExtensionObjectViewModel : ObservableObject
{
    public WeakReference<IPageContext> PageContext { get; set; }

    public ExtensionObjectViewModel(IPageContext? context)
    {
        var realContext = context ?? (this is IPageContext c ? c : throw new ArgumentException("You need to pass in an IErrorContext"));
        PageContext = new(realContext);
    }

    public ExtensionObjectViewModel(WeakReference<IPageContext> context)
    {
        PageContext = context;
    }

    public async virtual Task InitializePropertiesAsync()
    {
        var t = new Task(() =>
        {
            SafeInitializePropertiesSynchronous();
        });
        t.Start();
        await t;
    }

    public void SafeInitializePropertiesSynchronous()
    {
        try
        {
            InitializeProperties();
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    public abstract void InitializeProperties();

    protected void UpdateProperty(string propertyName)
    {
        DoOnUiThread(() => OnPropertyChanged(propertyName));
    }

    protected void ShowException(Exception ex, string? extensionHint = null)
    {
        if (PageContext.TryGetTarget(out var pageContext))
        {
            pageContext.ShowException(ex, extensionHint);
        }
    }

    protected void DoOnUiThread(Action action)
    {
        if (PageContext.TryGetTarget(out var pageContext))
        {
            Task.Factory.StartNew(
                action,
                CancellationToken.None,
                TaskCreationOptions.None,
                pageContext.Scheduler);
        }
    }

    protected virtual void UnsafeCleanup()
    {
        // base doesn't do anything, but sub-classes should override this.
    }

    public virtual void SafeCleanup()
    {
        try
        {
            UnsafeCleanup();
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex.ToString());
        }
    }
}
