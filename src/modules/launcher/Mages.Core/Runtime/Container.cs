namespace Mages.Core.Runtime
{
    using Mages.Core.Runtime.Converters;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A global accessible container for engine-independent services.
    /// </summary>
    public static class Container
    {
        private static readonly List<Object> _container = new List<Object>
        {
            CamelNameSelector.Instance,
        };

        /// <summary>
        /// Registers the specified service in the container.
        /// </summary>
        /// <typeparam name="T">The type of service to set.</typeparam>
        /// <param name="service">The service to register.</param>
        /// <returns>The optional lifetime controlling instance.</returns>
        public static IDisposable Register<T>(T service)
        {
            if (service != null && !_container.Contains(service))
            {
                _container.Add(service);
                return new ServiceLifeTime(service);
            }

            return null;
        }

        /// <summary>
        /// Unregisters the specified service from the container.
        /// </summary>
        /// <typeparam name="T">The type of service to remove.</typeparam>
        /// <param name="service">The service to remove.</param>
        /// <returns>True if the service was removed, otherwise false.</returns>
        public static Boolean Unregister<T>(T service)
        {
            return _container.Remove(service);
        }

        /// <summary>
        /// Unregisters the specified service from the container.
        /// </summary>
        /// <typeparam name="T">The type of service to remove.</typeparam>
        /// <returns>The removed services, if any.</returns>
        public static IEnumerable<T> Unregister<T>()
        {
            var services = new List<T>(GetAllServices<T>());

            foreach (var service in services)
            {
                _container.Remove(service);
            }

            return services;
        }

        /// <summary>
        /// Tries to get the specified service from the container.
        /// </summary>
        /// <typeparam name="T">The type of service to get.</typeparam>
        /// <param name="defaultValue">The optional default instance.</param>
        /// <returns>The service or a default instance.</returns>
        public static T GetService<T>(T defaultValue = default(T))
        {
            for (var i = _container.Count - 1; i >= 0; i--)
            {
                var service = _container[i];

                if (service is T)
                {
                    return (T)service;
                }   
            }

            return defaultValue;
        }

        /// <summary>
        /// Tries to get the specified services from the container.
        /// </summary>
        /// <typeparam name="T">The type of service to get.</typeparam>
        /// <returns>The services.</returns>
        public static IEnumerable<T> GetAllServices<T>()
        {
            for (var i = 0; i < _container.Count; i++)
            {
                var service = _container[i];

                if (service is T)
                {
                    yield return (T)service;
                }
            }
        }

        sealed class ServiceLifeTime : IDisposable
        {
            private Object _service;

            public ServiceLifeTime(Object service)
            {
                _service = service;
            }

            public void Dispose()
            {
                Container.Unregister(_service);
            }
        }
    }
}
