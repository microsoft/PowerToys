// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Controls;

namespace WorkspacesEditor.Controls
{
    public class ResetIsEnabled : ContentControl
    {
        static ResetIsEnabled()
        {
            IsEnabledProperty.OverrideMetadata(
                typeof(ResetIsEnabled),
                new UIPropertyMetadata(
                    defaultValue: true,
                    propertyChangedCallback: (_, __) => { },
                    coerceValueCallback: (_, x) => x));
        }
    }
}
