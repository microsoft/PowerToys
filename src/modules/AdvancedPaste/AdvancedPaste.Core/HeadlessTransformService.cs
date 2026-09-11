// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

namespace AdvancedPaste.Core;

public static class HeadlessTransformService
{
    public static string Transform(HeadlessTransformFormat format, string input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        return format switch
        {
            HeadlessTransformFormat.PlainText => input,
            HeadlessTransformFormat.Markdown => MarkdownConverter.Convert(input, cancellationToken),
            HeadlessTransformFormat.Json => JsonConverter.Convert(input, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported headless transform format."),
        };
    }
}
