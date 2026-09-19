// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToysExtension.Helpers;

internal sealed class PowerDisplayCliResult<T>
    where T : class
{
    private PowerDisplayCliResult(
        T? value,
        PowerDisplayCliFailureKind failureKind,
        string errorMessage)
    {
        Value = value;
        FailureKind = failureKind;
        ErrorMessage = errorMessage;
    }

    internal bool IsSuccess => FailureKind == PowerDisplayCliFailureKind.None && Value is not null;

    internal T? Value { get; }

    internal PowerDisplayCliFailureKind FailureKind { get; }

    internal string ErrorMessage { get; }

    internal static PowerDisplayCliResult<T> Success(T value)
        => new(value, PowerDisplayCliFailureKind.None, string.Empty);

    internal static PowerDisplayCliResult<T> Failure(
        PowerDisplayCliFailureKind failureKind,
        string errorMessage = "")
        => new(null, failureKind, errorMessage);
}
