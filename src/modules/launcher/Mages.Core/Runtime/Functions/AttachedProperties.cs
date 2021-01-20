namespace Mages.Core.Runtime.Functions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The collection of all attached properties.
    /// </summary>
    public static class AttachedProperties
    {
        private static readonly Dictionary<Type, Dictionary<String, Func<Object, Object>>> _properties = new Dictionary<Type, Dictionary<String, Func<Object, Object>>>
        {
            { typeof(Function), CreateFunctionProperties() }
        };

        /// <summary>
        /// Registers the provided attached property.
        /// </summary>
        /// <typeparam name="T">The type of the object to extend.</typeparam>
        /// <param name="name">The name of the property to attach.</param>
        /// <param name="getter">The getter function to register.</param>
        public static void Register<T>(String name, Func<T, Object> getter)
        {
            var properties = default(Dictionary<String, Func<Object, Object>>);

            if (!_properties.TryGetValue(typeof(T), out properties))
            {
                properties = new Dictionary<String, Func<Object, Object>>();
                _properties.Add(typeof(T), properties);
            }

            properties[name] = val => getter((T)val);
        }

        /// <summary>
        /// Unregisters the provided attached property.
        /// </summary>
        /// <typeparam name="T">The type of the object to extend.</typeparam>
        /// <param name="name">The name of the property to detach.</param>
        public static void Unregister<T>(String name)
        {
            var properties = default(Dictionary<String, Func<Object, Object>>);

            if (_properties.TryGetValue(typeof(T), out properties) && properties.Remove(name) && properties.Count == 0)
            {
                _properties.Remove(typeof(T));
            }
        }

        /// <summary>
        /// Tries to find the value for the attached property.
        /// </summary>
        /// <param name="instance">The object context.</param>
        /// <param name="name">The name of the property to retrieve.</param>
        /// <param name="value">The potentially attached property value.</param>
        /// <returns>True if the attached property could be found, otherwise false.</returns>
        public static Boolean TryFind(Object instance, String name, out Object value)
        {
            var type = instance.GetType();

            foreach (var property in _properties)
            {
                if (property.Key.IsAssignableFrom(type))
                {
                    var getter = default(Func<Object, Object>);

                    if (property.Value.TryGetValue(name, out getter))
                    {
                        value = getter(instance);
                        return true;
                    }

                    break;
                }
            }

            value = null;
            return false;
        }

        private static Dictionary<String, Func<Object, Object>> CreateFunctionProperties()
        {
            return new Dictionary<String, Func<Object, Object>>
            {
                { "params", obj => ((Function)obj).GetParameterNames().ToArrayObject() }
            };
        }
    }
}
