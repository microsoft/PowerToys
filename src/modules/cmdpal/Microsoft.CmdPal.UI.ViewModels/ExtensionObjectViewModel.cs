// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

public abstract partial class ExtensionObjectViewModel : ObservableObject
{
    public IErrorContext ErrorContext { get; set; }

    public ExtensionObjectViewModel(IErrorContext? errorContext)
    {
        if (errorContext != null)
        {
            ErrorContext = errorContext;
        }
        else
        {
            ErrorContext = this is IErrorContext context ? context : throw new ArgumentException("You need to pass in an IErrorContext");
        }
    }

    public async virtual Task InitializePropertiesAsync()
    {
        var t = new Task(() =>
        {
            try
            {
                InitializeProperties();
            }
            catch (Exception ex)
            {
                ErrorContext.ShowException(ex);
            }
        });
        t.Start();
        await t;
    }

    public abstract void InitializeProperties();
}
