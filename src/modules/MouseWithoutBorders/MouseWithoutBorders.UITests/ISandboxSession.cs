// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;

namespace Microsoft.MouseWithoutBorders.UITests;

internal interface ISandboxSession
{
    string Backend { get; }

    IReadOnlyList<ProcessIdentity> Processes { get; }

    long ViewerHwnd { get; }

    bool GuestAcknowledged { get; }

    JsonObject? RecoveryState { get; }

    void Start(string configurationPath, string productArchive, string payloadRoot, string toolsRoot, EndpointChannel guest);

    void Discover();

    void AcknowledgeGuest();

    void CaptureEvidence();

    void Stop();
}
