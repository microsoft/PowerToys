// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Common.Native;

[GeneratedComInterface]
[Guid("D5F951D9-661B-51E0-9CB8-D83BEF0098E4")]
public partial interface IExtension
{
    IntPtr GetProvider(ProviderType providerType);

    void Dispose();
}
