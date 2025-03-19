// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.CmdPal.Common.Extensions;

public static class IHostExtensions
{
    /// <summary>
    /// <inheritdoc cref="ActivatorUtilities.CreateInstance(IServiceProvider, Type, object[])"/>
    /// </summary>
    public static T CreateInstance<T>(this IHost host, params object[] parameters)
    {
        return ActivatorUtilities.CreateInstance<T>(host.Services, parameters);
    }

    /// <summary>
    /// Gets the service object for the specified type, or throws an exception
    /// if type was not registered.
    /// </summary>
    /// <typeparam name="T">Service type</typeparam>
    /// <param name="host">Host object</param>
    /// <returns>Service object</returns>
    /// <exception cref="ArgumentException">Throw an exception if the specified
    /// type is not registered</exception>
    public static T GetService<T>(this IHost host)
        where T : class
    {
        if (host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices.");
        }

        return service;
    }
}
