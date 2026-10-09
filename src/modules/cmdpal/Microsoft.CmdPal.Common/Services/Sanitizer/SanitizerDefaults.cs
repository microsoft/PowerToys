// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;

namespace Microsoft.CmdPal.Common.Services.Sanitizer;

internal static class SanitizerDefaults
{
    public const RegexOptions DefaultOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;
    public const int DefaultMatchTimeoutMs = 100;
}
