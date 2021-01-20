namespace Mages.Core
{
    using Mages.Core.Runtime;
    using Mages.Core.Runtime.Converters;
    using System;

    /// <summary>
    /// Defines the function delegate.
    /// </summary>
    /// <param name="args">The arguments to pass in.</param>
    /// <returns>The result of the evaluation.</returns>
    public delegate Object Function(Object[] args);

    /// <summary>
    /// A set of useful extensions for functions.
    /// </summary>
    public static class FunctionExtensions
    {
        /// <summary>
        /// Calls the function with the given arguments.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="arguments">The arguments to supply.</param>
        /// <returns>The result of calling the function.</returns>
        public static Object Call(this Function function, params Object[] arguments)
        {
            var length = arguments.Length;

            for (var i = 0; i < length; i++)
            {
                var argument = arguments[i];

                if (argument != null)
                {
                    var from = argument.GetType();
                    var type = from.FindPrimitive();
                    var converter = Helpers.Converters.FindConverter(from, type);
                    arguments[i] = converter.Invoke(argument);
                }
            }

            return function.Invoke(arguments);
        }

        /// <summary>
        /// Calls the function with the given arguments.
        /// </summary>
        /// <typeparam name="TResult">The anticipated result type.</typeparam>
        /// <param name="function">The function.</param>
        /// <param name="arguments">The arguments to supply.</param>
        /// <returns>The result or the type's default value.</returns>
        public static TResult Call<TResult>(this Function function, params Object[] arguments)
        {
            var result = function.Call(arguments);

            if (result is TResult)
            {
                return (TResult)result;
            }

            return default(TResult);
        }
        
        /// <summary>
        /// Gets the names of the parameters of the function.
        /// </summary>
        /// <param name="function">The function to inspect.</param>
        /// <returns>The array with parameter names.</returns>
        public static String[] GetParameterNames(this Function function)
        {
            var local = function.Target as LocalFunction;
            var parameters = local?.Parameters;

            if (parameters == null)
            {
                var nativeParams = function.Method.GetParameters();
                var parameterNames = new String[nativeParams.Length];

                if (nativeParams.Length == 1 && nativeParams[0].ParameterType == typeof(Object[]))
                {
                    parameterNames[0] = "..." + nativeParams[0].Name;
                }
                else
                {
                    for (var i = 0; i < parameterNames.Length; i++)
                    {
                        parameterNames[i] = nativeParams[i].Name;
                    }
                }

                return parameterNames;
            }
            else
            {
                var parameterNames = new String[parameters.Length];

                for (var i = 0; i < parameterNames.Length; i++)
                {
                    parameterNames[i] = parameters[i].Name;
                }

                return parameterNames;
            }
        }
    }
}
