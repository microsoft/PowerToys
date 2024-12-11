// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

public abstract partial class ExtensionObjectViewModel : ObservableObject
{
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
                System.Diagnostics.Debug.WriteLine(ex);
            }
        });
        t.Start();
        await t;
    }

    public abstract void InitializeProperties();
}
