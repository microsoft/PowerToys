// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public abstract partial class ContentViewModel(WeakReference<IPageContext> context) :
    ExtensionObjectViewModel(context)
{
    private INotifyPropChanged? _observable;
    private bool _initialized;

    private protected ViewModelLifetime Lifetime { get; } = new();

    protected abstract INotifyPropChanged? ObservableModel { get; }

    public bool OnlyControlOnPage { get; internal set; }

    public sealed override void InitializeProperties() => Lifetime.Run(() =>
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            var observable = ObservableModel;
            if (observable is not null)
            {
                observable.PropChanged += Model_PropChanged;
                _observable = observable;
            }

            InitializeContent();
            _initialized = true;
        }
        catch
        {
            Lifetime.Close(Cleanup);
            throw;
        }
    });

    protected abstract void InitializeContent();

    protected abstract void FetchProperty(string propertyName);

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        try
        {
            var propertyName = args.PropertyName;
            Lifetime.Run(() => FetchProperty(propertyName));
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    protected sealed override void UnsafeCleanup() => Lifetime.Close(Cleanup);

    private void Cleanup()
    {
        var observable = _observable;
        _observable = null;
        _initialized = false;
        try
        {
            if (observable is not null)
            {
                observable.PropChanged -= Model_PropChanged;
            }
        }
        finally
        {
            CleanupContent();
        }
    }

    protected virtual void CleanupContent()
    {
    }
}
