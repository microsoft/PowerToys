// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
namespace PowerDisplay.Contracts;

public sealed class ApplyProfileRequest
{
    public string ProfileName { get; set; } = string.Empty;
}
