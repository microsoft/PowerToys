// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// Provides a static wrapper around <see cref="Microsoft.Extensions.DependencyInjection.ServiceProvider"/>
    /// to enable gradual migration to dependency injection.
    /// </summary>
    public static class ServiceProvider
    {
        private static readonly object _lock = new object();
        private static IServiceProvider _serviceProvider;

        /// <summary>
        /// Gets a value indicating whether the service provider has been initialized.
        /// </summary>
        public static bool IsInitialized => _serviceProvider != null;

        /// <summary>
        /// Initializes the service provider with the specified service collection.
        /// This should be called once during application startup.
        /// </summary>
        /// <param name="services">The service collection containing all registered services.</param>
        /// <exception cref="InvalidOperationException">Thrown if the service provider is already initialized.</exception>
        public static void Initialize(IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            lock (_lock)
            {
                if (_serviceProvider != null)
                {
                    throw new InvalidOperationException("ServiceProvider is already initialized.");
                }

                _serviceProvider = services.BuildServiceProvider();
            }
        }

        /// <summary>
        /// Gets a service of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of service to get.</typeparam>
        /// <returns>The service instance, or null if not registered.</returns>
        public static T GetService<T>()
            where T : class
        {
            EnsureInitialized();
            return _serviceProvider.GetService<T>();
        }

        /// <summary>
        /// Gets a required service of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of service to get.</typeparam>
        /// <returns>The service instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not registered.</exception>
        public static T GetRequiredService<T>()
            where T : class
        {
            EnsureInitialized();
            return _serviceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Gets a service of the specified type.
        /// </summary>
        /// <param name="serviceType">The type of service to get.</param>
        /// <returns>The service instance, or null if not registered.</returns>
        public static object GetService(Type serviceType)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            EnsureInitialized();
            return _serviceProvider.GetService(serviceType);
        }

        /// <summary>
        /// Gets a required service of the specified type.
        /// </summary>
        /// <param name="serviceType">The type of service to get.</param>
        /// <returns>The service instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not registered.</exception>
        public static object GetRequiredService(Type serviceType)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            EnsureInitialized();
            return _serviceProvider.GetRequiredService(serviceType);
        }

        /// <summary>
        /// Creates a new scope for scoped services.
        /// </summary>
        /// <returns>A new service scope.</returns>
        public static IServiceScope CreateScope()
        {
            EnsureInitialized();
            return _serviceProvider.CreateScope();
        }

        private static void EnsureInitialized()
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider has not been initialized. Call Initialize() first.");
            }
        }
    }
}
