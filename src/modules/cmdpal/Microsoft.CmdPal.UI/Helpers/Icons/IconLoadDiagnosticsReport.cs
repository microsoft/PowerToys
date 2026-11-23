// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed record IconLoadDiagnosticsReport(
    long SessionId,
    DateTimeOffset StartedUtc,
    DateTimeOffset EndedUtc,
    TimeSpan Duration,
    string Text);
