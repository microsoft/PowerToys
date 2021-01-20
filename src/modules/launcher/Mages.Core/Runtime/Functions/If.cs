namespace Mages.Core.Runtime.Functions
{
    using System;

    /// <summary>
    /// Helpers to check for argument types.
    /// </summary>
    public static class If
    {
        /// <summary>
        /// Checks if the first value of the given arguments is of type T.
        /// </summary>
        /// <typeparam name="T">The type T to check for.</typeparam>
        /// <param name="args">The arguments to check.</param>
        /// <param name="f">The callback to invoke if fulfilled.</param>
        /// <returns>The result of the callback or null.</returns>
        public static Object Is<T>(Object[] args, Func<T, Object> f)
        {
            return args[0] is T ? f((T)args[0]) : null;
        }

        /// <summary>
        /// Checks if the first two values of the given arguments are of type
        /// T1 and T2.
        /// </summary>
        /// <typeparam name="T1">The type of the first value.</typeparam>
        /// <typeparam name="T2">The type of the second value.</typeparam>
        /// <param name="args">The arguments to check.</param>
        /// <param name="f">The callback to invoke if fulfilled.</param>
        /// <returns>The result of the callback or null.</returns>
        public static Object Is<T1, T2>(Object[] args, Func<T1, T2, Object> f)
        {
            return args[0] is T1 && args[1] is T2 ? f((T1)args[0], (T2)args[1]) : null;
        }

        /// <summary>
        /// Checks if the first three values of the given arguments are of type
        /// T1, T2, and T3.
        /// </summary>
        /// <typeparam name="T1">The type of the first value.</typeparam>
        /// <typeparam name="T2">The type of the second value.</typeparam>
        /// <typeparam name="T3">The type of the third value.</typeparam>
        /// <param name="args">The arguments to check.</param>
        /// <param name="f">The callback to invoke if fulfilled.</param>
        /// <returns>The result of the callback or null.</returns>
        public static Object Is<T1, T2, T3>(Object[] args, Func<T1, T2, T3, Object> f)
        {
            return args[0] is T1 && args[1] is T2 && args[2] is T3 ? f((T1)args[0], (T2)args[1], (T3)args[2]) : null;
        }

        /// <summary>
        /// Checks if the first four values of the given arguments are of type
        /// T1, T2, T3, and T4.
        /// </summary>
        /// <typeparam name="T1">The type of the first value.</typeparam>
        /// <typeparam name="T2">The type of the second value.</typeparam>
        /// <typeparam name="T3">The type of the third value.</typeparam>
        /// <typeparam name="T4">The type of the fourth value.</typeparam>
        /// <param name="args">The arguments to check.</param>
        /// <param name="f">The callback to invoke if fulfilled.</param>
        /// <returns>The result of the callback or null.</returns>
        public static Object Is<T1, T2, T3, T4>(Object[] args, Func<T1, T2, T3, T4, Object> f)
        {
            return args[0] is T1 && args[1] is T2 && args[2] is T3 && args[3] is T4 ? 
                f((T1)args[0], (T2)args[1], (T3)args[2], (T4)args[3]) : null;
        }

        /// <summary>
        /// Checks if the first three values of the given arguments are of type
        /// T1, anything, and T3.
        /// </summary>
        /// <typeparam name="T1">The type of the first value.</typeparam>
        /// <typeparam name="T3">The type of the third value.</typeparam>
        /// <param name="args">The arguments to check.</param>
        /// <param name="f">The callback to invoke if fulfilled.</param>
        /// <returns>The result of the callback or null.</returns>
        public static Object IsAnyT2<T1, T3>(Object[] args, Func<T1, Object, T3, Object> f)
        {
            return args[0] is T1 && args[2] is T3 ? f((T1)args[0], args[1], (T3)args[2]) : null;
        }

        /// <summary>
        /// Checks if the provided args are all non-null. Performs the conversion and invokes
        /// the result if successful.
        /// </summary>
        /// <typeparam name="T">The type after the conversion.</typeparam>
        /// <param name="args">The arguments to check.</param>
        /// <param name="converter">The converter to use.</param>
        /// <param name="result">The result function to use.</param>
        /// <returns>The result or null if the check was not successful.</returns>
        public static Object IsNotNull<T>(Object[] args, Func<Object, T> converter, Func<T, T, Object> result)
        {
            if (args[0] != null && args[1] != null)
            {
                var left = converter.Invoke(args[0]);
                var right = converter.Invoke(args[1]);
                return result.Invoke(left, right);
            }

            return null;
        }
    }
}
