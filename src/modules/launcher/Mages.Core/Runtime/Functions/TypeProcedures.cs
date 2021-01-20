namespace Mages.Core.Runtime.Functions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The collection of all type function creators.
    /// </summary>
    public static class TypeProcedures
    {
        private static Dictionary<Type, Func<Object, Procedure>> _setters = new Dictionary<Type, Func<Object, Procedure>>
        {
            { typeof(Double[,]), obj => ((Double[,])obj).Setter },
            { typeof(IDictionary<String, Object>), obj => ((IDictionary<String, Object>)obj).Setter },
        };

        /// <summary>
        /// Registers the provided setter procedure.
        /// </summary>
        /// <typeparam name="T">The type of the object to extend.</typeparam>
        /// <param name="setter">The setter function to register.</param>
        public static void Register<T>(Func<T, Procedure> setter)
        {
            _setters[typeof(T)] = val => setter((T)val);
        }

        /// <summary>
        /// Unregisters the type function for the given type.
        /// </summary>
        /// <typeparam name="T">The type of object to unconfigure.</typeparam>
        public static void Unregister<T>()
        {
            _setters.Remove(typeof(T));
        }

        /// <summary>
        /// Tries to find the named setter.
        /// </summary>
        /// <param name="instance">The object context.</param>
        /// <param name="procedure">The potentially found setter procedure.</param>
        /// <returns>True if the setter could be found, otherwise false.</returns>
        public static Boolean TryFind(Object instance, out Procedure procedure)
        {
            var type = instance.GetType();

            foreach (var setter in _setters)
            {
                if (setter.Key.IsAssignableFrom(type))
                {
                    procedure = setter.Value.Invoke(instance);
                    return true;
                }
            }

            procedure = null;
            return false;
        }
    }
}
