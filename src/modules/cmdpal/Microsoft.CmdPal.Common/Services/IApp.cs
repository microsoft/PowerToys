// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
