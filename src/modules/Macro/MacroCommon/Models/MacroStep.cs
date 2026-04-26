// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.MacroCommon.Models;

public sealed record MacroStep
{
    public StepType Type { get; init; }

    public string? Key { get; init; }

    public string? Text { get; init; }

    public int? Ms { get; init; }

    public int? Count { get; init; }

    public List<MacroStep>? Steps { get; init; }
}
