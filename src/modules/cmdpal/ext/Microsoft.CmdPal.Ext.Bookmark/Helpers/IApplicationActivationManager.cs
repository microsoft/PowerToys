// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D")]
internal partial interface IApplicationActivationManager
{
    void ActivateApplication(string appUserModelId, string arguments, int options, out uint processId);
}
