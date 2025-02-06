// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerLauncher.Services;

public static class RegistryServiceFactory
{
    public static IRegistryService Create()
    {
        return new RegistryService();
    }
}
