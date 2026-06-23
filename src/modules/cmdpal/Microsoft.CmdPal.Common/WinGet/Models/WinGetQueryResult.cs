// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.WinGet.Models;

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1649:File name should match first type name", Justification = "Generic result type.")]
public sealed record WinGetQueryResult<T>(
    T? Value,
    bool IsUnavailable,
    string? ErrorMessage)
{
    public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage);
}
