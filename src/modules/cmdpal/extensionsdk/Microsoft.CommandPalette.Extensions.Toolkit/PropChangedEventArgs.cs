// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class PropChangedEventArgs : IPropChangedEventArgs
{
    public string PropertyName { get; private set; }

    public PropChangedEventArgs(string propertyName)
    {
        PropertyName = propertyName;
    }
}
