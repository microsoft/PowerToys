// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser.Providers;

/// <summary>
/// Retrieves the default web browser using the system shell functions.
/// </summary>
internal sealed class ShellAssociationProvider : AssociationProviderBase
{
    private static readonly string[] Protocols = ["https", "http"];

    protected override AssociatedApp FindAssociation()
    {
        foreach (var protocol in Protocols)
        {
            var command = AssocQueryStringSafe(NativeMethods.AssocStr.Command, protocol);
            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            var appName = AssocQueryStringSafe(NativeMethods.AssocStr.FriendlyAppName, protocol);

            return new AssociatedApp(command, appName);
        }

        return new AssociatedApp(null, null);
    }

    private static unsafe string? AssocQueryStringSafe(NativeMethods.AssocStr what, string protocol)
    {
        uint cch = 0;

        // First call: get required length (incl. null)
        _ = NativeMethods.AssocQueryStringW(NativeMethods.AssocF.IsProtocol, what, protocol, null, null, ref cch);
        if (cch == 0)
        {
            return null;
        }

        // Small buffers on stack; large on heap
        var span = cch <= 512 ? stackalloc char[(int)cch] : new char[(int)cch];

        fixed (char* p = span)
        {
            var hr = NativeMethods.AssocQueryStringW(NativeMethods.AssocF.IsProtocol, what, protocol, null, p, ref cch);
            if (hr != 0 || cch == 0)
            {
                return null;
            }

            // cch includes the null terminator; slice it off
            var len = (int)cch - 1;
            if (len < 0)
            {
                len = 0;
            }

            return new string(span[..len]);
        }
    }
}
