// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Services.Sanitizer.Abstraction;

namespace Microsoft.CmdPal.Core.Common.Services.Sanitizer;

internal interface ISanitizationRuleProvider
{
    IEnumerable<SanitizationRule> GetRules();
}
