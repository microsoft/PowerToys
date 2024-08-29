// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.CmdPal.Common.Services;

/// <summary>
/// Interface for the current application singleton object exposing the API
/// that can be accessed from anywhere in the application.
/// </summary>
public interface IApp
{
    /// <summary>
    /// Gets services registered at the application level.
    /// </summary>
    public T GetService<T>()
        where T : class;
}
