// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.ViewModels.Messages;

/// <summary>
/// Message containing the current navigation depth (BackStack count) when navigating to a page.
/// Used to track maximum navigation depth reached during a session for telemetry.
/// </summary>
public record NavigationDepthMessage(int Depth);
