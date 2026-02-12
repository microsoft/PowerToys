// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.Common.Services.Sanitizer;

public record GuardrailEventArgs(
    string RuleDescription,
    int OriginalLength,
    int ResultLength,
    double Threshold)
{
    public double Ratio => OriginalLength > 0 ? (double)ResultLength / OriginalLength : 1.0;
}
