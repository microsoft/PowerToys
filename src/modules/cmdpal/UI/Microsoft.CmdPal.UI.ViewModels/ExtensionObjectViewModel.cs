// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.UI.ViewModels;

public abstract partial class ExtensionObjectViewModel : ObservableObject
{
    private readonly ILogger logger;

    public WeakReference<IPageContext> PageContext { get; set; }

    internal ExtensionObjectViewModel(IPageContext? context, ILogger logger)
    {
        this.logger = logger;
        var realContext = context ?? (this is IPageContext c ? c : throw new ArgumentException("You need to pass in an IErrorContext"));
        PageContext = new(realContext);
    }

    internal ExtensionObjectViewModel(WeakReference<IPageContext> context, ILogger logger)
    {
        this.logger = logger;
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

    protected void UpdateProperty(string propertyName1, string propertyName2)
    {
        DoOnUiThread(() =>
        {
            OnPropertyChanged(propertyName1);
            OnPropertyChanged(propertyName2);
        });
    }

    protected void UpdateProperty(string propertyName1, string propertyName2, string propertyName3)
    {
        DoOnUiThread(() =>
        {
            OnPropertyChanged(propertyName1);
            OnPropertyChanged(propertyName2);
            OnPropertyChanged(propertyName3);
        });
    }

    protected void UpdateProperty(params string[] propertyNames)
    {
        DoOnUiThread(() =>
        {
            foreach (var propertyName in propertyNames)
            {
                OnPropertyChanged(propertyName);
            }
        });
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
            Log_ExtensionObjectViewModelFailedCleanup(ex.ToString());
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "{message}")]
    partial void Log_ExtensionObjectViewModelFailedCleanup(string message);
}
