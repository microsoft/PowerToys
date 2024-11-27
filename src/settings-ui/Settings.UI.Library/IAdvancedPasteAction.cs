// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;

namespace Microsoft.PowerToys.Settings.UI.Library;

public interface IAdvancedPasteAction : INotifyPropertyChanged
{
    public bool IsShown { get; }

    public IEnumerable<IAdvancedPasteAction> SubActions { get; }
}
