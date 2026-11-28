// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>UI Automation's IRawElementProviderSimple, as UIAutomationCore.h lays it out, for the notice's provider.</summary>
[GeneratedComInterface]
[Guid("d6dd68d1-86fd-4332-8666-9abedea2d24c")]
internal unsafe partial interface IRawElementProviderSimple
{
    [PreserveSig]
    int GetProviderOptions(out int options);

    [PreserveSig]
    int GetPatternProvider(int patternId, out IntPtr provider);

    // A pointer, because marshalling a VARIANT by value needs runtime marshalling off for the whole engine.
    [PreserveSig]
    int GetPropertyValue(int propertyId, ComVariant* value);

    [PreserveSig]
    int GetHostRawElementProvider(out IntPtr provider);
}
