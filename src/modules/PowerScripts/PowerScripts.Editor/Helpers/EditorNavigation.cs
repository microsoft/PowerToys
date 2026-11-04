// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Controls;

namespace PowerScripts.Editor.Helpers;

internal static class EditorNavigation
{
    internal static bool GoBack(Frame frame)
    {
        if (!frame.CanGoBack)
        {
            return false;
        }

        frame.GoBack();
        return true;
    }
}
