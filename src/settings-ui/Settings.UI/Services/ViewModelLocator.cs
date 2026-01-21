// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// Provides lazy service resolution with optional caching.
    /// Used by Views to obtain services without direct DI container access.
    /// </summary>
    public class ViewModelLocator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<Type, object> _cachedServices;
        private readonly bool _enableCaching;

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelLocator"/> class.
        /// </summary>
        /// <param name="serviceProvider">The DI service provider.</param>
        /// <param name="enableCaching">Whether to cache service instances.</param>
        public ViewModelLocator(IServiceProvider serviceProvider, bool enableCaching = false)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _enableCaching = enableCaching;
            _cachedServices = enableCaching ? new ConcurrentDictionary<Type, object>() : null;
        }

        /// <summary>
        /// Gets or creates a service of the specified type.
        /// </summary>
        /// <typeparam name="TService">The type of service to get.</typeparam>
        /// <returns>The service instance.</returns>
        public TService GetService<TService>()
            where TService : class
        {
            return (TService)GetService(typeof(TService));
        }

        /// <summary>
        /// Gets or creates a service of the specified type.
        /// </summary>
        /// <param name="serviceType">The type of service to get.</param>
        /// <returns>The service instance.</returns>
        public object GetService(Type serviceType)
        {
            if (_enableCaching && _cachedServices != null)
            {
                return _cachedServices.GetOrAdd(serviceType, type =>
                    _serviceProvider.GetService(type));
            }

            return _serviceProvider.GetService(serviceType);
        }

        /// <summary>
        /// Clears cached services (if caching is enabled).
        /// </summary>
        public void ClearCache()
        {
            _cachedServices?.Clear();
        }

        /// <summary>
        /// Removes a specific service from the cache (if caching is enabled).
        /// </summary>
        /// <typeparam name="TService">The type of service to remove.</typeparam>
        public void RemoveFromCache<TService>()
            where TService : class
        {
            _cachedServices?.TryRemove(typeof(TService), out _);
        }

        // Convenience properties for common services

        /// <summary>
        /// Gets the navigation service.
        /// </summary>
        public INavigationService NavigationService => GetService<INavigationService>();
    }
}
