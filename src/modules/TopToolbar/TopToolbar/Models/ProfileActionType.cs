// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace TopToolbar.Models;

/// <summary>
/// Types of actions that can be executed by profiles
/// </summary>
public enum ProfileActionType
{
    /// <summary>
    /// Execute a command line application
    /// </summary>
    CommandLine,

    /// <summary>
    /// Call an action provider
    /// </summary>
    Provider,

    /// <summary>
    /// Chat-based action (future)
    /// </summary>
    Chat,
}
