// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.AI;

/// <summary>
/// Exception thrown when an AI provider fails to generate a response.
/// </summary>
public sealed class AIGenerationException : Exception
{
    public AIGenerationException(string message)
        : base(message)
    {
    }

    public AIGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
