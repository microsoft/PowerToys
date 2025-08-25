// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.RegularExpressions;

using Microsoft.CmdPal.Common.Services.Sanitizer.Abstraction;

namespace Microsoft.CmdPal.Common.Services.Sanitizer;

internal sealed partial class TokenRuleProvider : ISanitizationRuleProvider
{
    public IEnumerable<SanitizationRule> GetRules()
    {
        yield return new(JwtRx(), "[JWT_REDACTED]", "JSON Web Tokens (JWT)");
        yield return new(TokenRx(), "[TOKEN_REDACTED]", "Potential API keys/tokens");
    }

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b",
        SanitizerDefaults.DefaultOptions, SanitizerDefaults.DefaultMatchTimeoutMs)]
    private static partial Regex JwtRx();

    [GeneratedRegex(@"\b[A-Za-z0-9]{32,128}\b",
        SanitizerDefaults.DefaultOptions, SanitizerDefaults.DefaultMatchTimeoutMs)]
    private static partial Regex TokenRx();
}
