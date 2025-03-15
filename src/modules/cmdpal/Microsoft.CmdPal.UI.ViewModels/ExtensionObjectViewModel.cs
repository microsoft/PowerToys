// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

public abstract partial class ExtensionObjectViewModel : ObservableObject
{
    public IPageContext PageContext { get; set; }

    public ExtensionObjectViewModel(IPageContext? context)
    {
        PageContext = context ?? (this is IPageContext c ? c : throw new ArgumentException("You need to pass in an IErrorContext"));
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
            PageContext.ShowException(ex);
        }
    }

    public abstract void InitializeProperties();

    protected void UpdateProperty(string propertyName) =>
        Task.Factory.StartNew(
            () =>
        {
            OnPropertyChanged(propertyName);
        },
            CancellationToken.None,
            TaskCreationOptions.None,
            PageContext.Scheduler);
}
