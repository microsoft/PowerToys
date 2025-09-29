// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.DSC.Models;

/// <summary>
/// Specifies the severity level of a message.
/// </summary>
public enum DscMessageLevel
{
    /// <summary>
    /// Represents an error message.
    /// </summary>
    Error,

    /// <summary>
    /// Represents a warning message.
    /// </summary>
    Warning,

    /// <summary>
    /// Represents an informational message.
    /// </summary>
    Info,

    /// <summary>
    /// Represents a debug message.
    /// </summary>
    Debug,

    /// <summary>
    /// Represents a trace message.
    /// </summary>
    Trace,
}
