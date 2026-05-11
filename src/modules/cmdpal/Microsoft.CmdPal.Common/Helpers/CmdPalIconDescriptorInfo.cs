// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Helpers;

public sealed record CmdPalIconDescriptorInfo(
    bool IsNil,
    IReadOnlyList<CmdPalIconSourceCandidate> Sources,
    IReadOnlyList<CmdPalIconSourceCandidate> LightSources,
    IReadOnlyList<CmdPalIconSourceCandidate> DarkSources);
