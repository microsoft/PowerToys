// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Helpers;

public readonly record struct CmdPalIconSourceCandidate(
    string Source,
    CmdPalIconSourceKind Kind = CmdPalIconSourceKind.Icon);
