// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Models;

public class ExtensionObject<T>(T? value) // where T : IInspectable
{
    public T? Unsafe { get; } = value;

    public override bool Equals(object? obj) => obj is ExtensionObject<T> ext && ext.Unsafe?.Equals(this.Unsafe) == true;

    public override int GetHashCode() => Unsafe?.GetHashCode() ?? base.GetHashCode();
}
