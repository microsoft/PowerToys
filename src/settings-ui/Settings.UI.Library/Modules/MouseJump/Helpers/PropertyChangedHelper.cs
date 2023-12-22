// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump.Helpers;

internal static class PropertyChangedHelper
{
    public static bool SetField<T>(ref T field, T value, Action<string>? propertyChanged, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        propertyChanged?.Invoke(propertyName);
        return true;
    }
}
