// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.Cli;

internal sealed record CliProviderDescriptor(string Id, string Name, string ServiceType, string Model, bool IsDefault, bool IsAllowedByPolicy);
