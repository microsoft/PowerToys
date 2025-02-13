// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerLauncher.Services;

/// <summary>
/// Factory for creating instances of <see cref="IRegistryService"/>.
/// </summary>
public static class RegistryServiceFactory
{
    /// <summary>
    /// Creates the default implementation of <see cref="IRegistryService"/>.
    /// </summary>
    /// <returns>An instance of the default <see cref="IRegistryService"/> implementation.</returns>
    public static IRegistryService Create() => new RegistryService();
}
