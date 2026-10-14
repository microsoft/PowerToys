// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

/// <summary>
/// Stable schema version stamped onto every IPC request and response envelope as informational
/// metadata. NOTE: neither side validates this today — a mismatched CLI/app currently surfaces as
/// a deserialization failure (INTERNAL_ERROR, exit 9), not a dedicated version error, and because
/// the source-gen serializer ignores unknown members, additive ("minor") drift is accepted
/// silently. Version negotiation (rejecting an incompatible major) is intentionally out of scope
/// for v1; wire it up here and in the dispatcher if forward-compat becomes a requirement.
/// </summary>
public static class CliSchema
{
    public const string Version = "1.0";
}
