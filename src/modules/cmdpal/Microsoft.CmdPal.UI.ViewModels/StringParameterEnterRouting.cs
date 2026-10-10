// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels;

public static class StringParameterEnterRouting
{
    public static StringParameterEnterAction GetAction(VirtualKey key, bool acceptsReturn, bool showCommand)
    {
        if (key != VirtualKey.Enter || acceptsReturn)
        {
            return StringParameterEnterAction.None;
        }

        return showCommand ? StringParameterEnterAction.Submit : StringParameterEnterAction.FocusNext;
    }
}
