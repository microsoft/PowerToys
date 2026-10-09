// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers.Analyzers;

internal sealed record ImageMetadata(
    uint Width,
    uint Height,
    double DpiX,
    double DpiY,
    ulong? StorageSize);
