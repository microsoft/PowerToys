// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>UI Automation's IWindowProvider, as UIAutomationCore.h lays it out, for the notice's provider.</summary>
[GeneratedComInterface]
[Guid("987df77b-db06-4d77-8f8a-86a9c3bb90b9")]
internal partial interface IWindowProvider
{
    [PreserveSig]
    int SetVisualState(int state);

    [PreserveSig]
    int Close();

    [PreserveSig]
    int WaitForInputIdle(int milliseconds, out int idle);

    [PreserveSig]
    int GetCanMaximize(out int canMaximize);

    [PreserveSig]
    int GetCanMinimize(out int canMinimize);

    [PreserveSig]
    int GetIsModal(out int isModal);

    [PreserveSig]
    int GetWindowVisualState(out int state);

    [PreserveSig]
    int GetWindowInteractionState(out int state);

    [PreserveSig]
    int GetIsTopmost(out int isTopmost);
}
