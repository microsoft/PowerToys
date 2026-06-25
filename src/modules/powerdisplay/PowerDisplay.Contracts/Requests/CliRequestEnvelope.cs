// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
namespace PowerDisplay.Contracts;

/// <summary>Top-level request envelope. Exactly one payload property is non-null,
/// selected by <see cref="Command"/>. Concrete payloads (not polymorphic object) keep AOT happy.</summary>
public sealed class CliRequestEnvelope
{
    public string Version { get; set; } = CliSchema.Version;

    public string Command { get; set; } = string.Empty;

    public ListRequest? List { get; set; }

    public GetRequest? Get { get; set; }

    public SetRequest? Set { get; set; }

    public CapabilitiesRequest? Capabilities { get; set; }

    public ProfilesRequest? Profiles { get; set; }

    public ApplyProfileRequest? ApplyProfile { get; set; }
}
