// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using Microsoft.CmdPal.Core.Common.Services.Sanitizer.Abstraction;

namespace Microsoft.CmdPal.Core.Common.Services.Sanitizer;

internal sealed partial class ConnectionStringRuleProvider : ISanitizationRuleProvider
{
    [GeneratedRegex(@"(Server|Data Source|Initial Catalog|Database|User ID|Username|Password|Pwd|Uid)\s*=\s*[^;,\s]+",
        SanitizerDefaults.DefaultOptions, SanitizerDefaults.DefaultMatchTimeoutMs)]
    private static partial Regex ConnectionParamRx();

    public IEnumerable<SanitizationRule> GetRules()
    {
        yield return new(ConnectionParamRx(), "$1=[REDACTED]", "Connection string parameters");
    }
}
