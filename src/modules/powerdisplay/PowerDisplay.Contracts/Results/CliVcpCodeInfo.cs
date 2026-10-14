// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Contracts;

public sealed class CliVcpCodeInfo
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public bool Continuous { get; init; }

    public IReadOnlyList<string>? DiscreteValues { get; init; }
}
