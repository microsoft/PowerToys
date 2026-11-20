// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Shell;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

internal static class ExplorerShellLauncher
{
    internal static unsafe void Launch(ProcessStartInfo startInfo)
    {
        var initialized = PInvoke.CoInitializeEx(null, COINIT.COINIT_MULTITHREADED);

        // An STA caller already has a usable COM apartment.
        if (initialized != HRESULT.RPC_E_CHANGED_MODE)
        {
            initialized.ThrowOnFailure();
        }

        IShellWindows* windows = null;
        IDispatch* desktop = null;
        IWebBrowser2* browser = null;
        IDispatch* document = null;
        IShellFolderViewDual* folder = null;
        IDispatch* application = null;
        IShellDispatch2* shell = null;
        try
        {
            PInvoke.CoCreateInstance(typeof(ShellWindows).GUID, null, CLSCTX.CLSCTX_LOCAL_SERVER, out windows).ThrowOnFailure();
            VARIANT location = default;
            location.Anonymous.Anonymous.vt = VARENUM.VT_I4;
            location.Anonymous.Anonymous.Anonymous.lVal = (int)PInvoke.CSIDL_DESKTOP;
            VARIANT root = default;
            windows->FindWindowSW(location, root, ShellWindowTypeConstants.SWC_DESKTOP, out _, ShellWindowFindWindowOptions.SWFO_NEEDDISPATCH, &desktop).ThrowOnFailure();
            if (desktop is null)
            {
                throw new InvalidOperationException("The desktop Explorer window is not available.");
            }

            // Use the running desktop's Application object, not a local Shell.Application.
            desktop->QueryInterface(out browser).ThrowOnFailure();
            browser->get_Document(&document).ThrowOnFailure();
            if (document is null)
            {
                throw new InvalidOperationException("The desktop Explorer document is not available.");
            }

            document->QueryInterface(out folder).ThrowOnFailure();
            folder->get_Application(&application).ThrowOnFailure();
            if (application is null)
            {
                throw new InvalidOperationException("The desktop Explorer application is not available.");
            }

            application->QueryInterface(out shell).ThrowOnFailure();

            var file = Marshal.StringToBSTR(startInfo.FileName);
            VARIANT arguments = default;
            VARIANT directory = default;
            VARIANT verb = default;
            try
            {
                arguments = StringVariant(startInfo.Arguments);
                directory = StringVariant(startInfo.WorkingDirectory);
                verb = StringVariant(startInfo.Verb);
                VARIANT show = default;
                show.Anonymous.Anonymous.vt = VARENUM.VT_I4;
                show.Anonymous.Anonymous.Anonymous.lVal = startInfo.WindowStyle == ProcessWindowStyle.Hidden ? 0 : 1;
                shell->ShellExecute(new BSTR((char*)file), arguments, directory, verb, show).ThrowOnFailure();
            }
            finally
            {
                Marshal.FreeBSTR(file);
                Marshal.FreeBSTR((nint)arguments.Anonymous.Anonymous.Anonymous.bstrVal.Value);
                Marshal.FreeBSTR((nint)directory.Anonymous.Anonymous.Anonymous.bstrVal.Value);
                Marshal.FreeBSTR((nint)verb.Anonymous.Anonymous.Anonymous.bstrVal.Value);
            }
        }
        finally
        {
            if (shell is not null)
            {
                shell->Release();
            }

            if (application is not null)
            {
                application->Release();
            }

            if (folder is not null)
            {
                folder->Release();
            }

            if (document is not null)
            {
                document->Release();
            }

            if (browser is not null)
            {
                browser->Release();
            }

            if (desktop is not null)
            {
                desktop->Release();
            }

            if (windows is not null)
            {
                windows->Release();
            }

            if (initialized.Succeeded)
            {
                PInvoke.CoUninitialize();
            }
        }
    }

    private static unsafe VARIANT StringVariant(string value)
    {
        VARIANT variant = default;
        variant.Anonymous.Anonymous.vt = VARENUM.VT_BSTR;
        variant.Anonymous.Anonymous.Anonymous.bstrVal = new BSTR((char*)Marshal.StringToBSTR(value));
        return variant;
    }
}
