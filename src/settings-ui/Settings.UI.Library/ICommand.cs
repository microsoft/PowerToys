// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// ICommand interface for WinUI 3 applications, compatible with Native AOT.
    /// Extends System.Windows.Input.ICommand which is also recognized by WinUI3 XAML bindings.
    /// </summary>
    public interface ICommand : System.Windows.Input.ICommand
    {
    }
}
