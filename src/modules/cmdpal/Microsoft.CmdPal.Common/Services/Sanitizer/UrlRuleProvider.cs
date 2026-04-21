// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using Microsoft.CmdPal.Common.Services.Sanitizer.Abstraction;

namespace Microsoft.CmdPal.Common.Services.Sanitizer;

internal sealed partial class UrlRuleProvider : ISanitizationRuleProvider
{
    public IEnumerable<SanitizationRule> GetRules()
    {
        yield return new(UrlRx(), "[URL_REDACTED]", "URLs");
    }

    [GeneratedRegex(@"\b(?:https?|ftp|ftps|file|jdbc|ldap|mailto)://[^\s<>""'{}\[\]\\^`|]+",
        SanitizerDefaults.DefaultOptions, SanitizerDefaults.DefaultMatchTimeoutMs)]
    private static partial Regex UrlRx();
}
