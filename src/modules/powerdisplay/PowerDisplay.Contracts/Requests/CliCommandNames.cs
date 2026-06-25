// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
namespace PowerDisplay.Contracts;

/// <summary>Canonical command discriminators shared by CLI and app.</summary>
public static class CliCommandNames
{
    public const string List = "list";
    public const string Get = "get";
    public const string Set = "set";
    public const string Capabilities = "capabilities";
    public const string Profiles = "profiles";
    public const string ApplyProfile = "apply-profile";
}
