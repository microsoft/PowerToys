// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Common.Search.FuzzSearch;

public class MatchOption
{
    public bool IgnoreCase { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to support Chinese PinYin
    /// </summary>
    public bool ChinesePinYinSupport { get; set; }
}
