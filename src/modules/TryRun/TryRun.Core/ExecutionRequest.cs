// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed record ExecutionRequest(string Script, string WorkingDirectory, string TemporaryDirectory, int TimeoutSeconds)
{
    public const int MaximumScriptLength = 8192;

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Script);
        if (Script.Length > MaximumScriptLength || Script.Contains('\0'))
        {
            throw new ArgumentException("The script must be at most 8192 characters and cannot contain null characters.");
        }

        if (TimeoutSeconds is < 1 or > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(TimeoutSeconds), "Choose a timeout between 1 and 300 seconds.");
        }

        ValidateDirectory(WorkingDirectory);
        ValidateDirectory(TemporaryDirectory);
    }

    private static void ValidateDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("The working and temporary directories must be absolute local drive paths.");
        }
    }
}
