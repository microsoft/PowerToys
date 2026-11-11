// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.Editor.Views;

namespace PowerScripts.Editor.Helpers;

internal static class EditorNavigation
{
    internal static bool GoBack(Frame frame)
    {
        if (!frame.CanGoBack || frame.Content is PowerScriptTemplatesPage { IsCreating: true })
        {
            return false;
        }

        frame.GoBack();
        return true;
    }

    internal static void OpenGlobalSettings()
    {
        var runner = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "PowerToys.exe"));
        using var process = Process.Start(new ProcessStartInfo(runner, "--open-settings=PowerScripts") { UseShellExecute = false })
            ?? throw new InvalidOperationException("Unable to open PowerToys Settings.");
    }
}
