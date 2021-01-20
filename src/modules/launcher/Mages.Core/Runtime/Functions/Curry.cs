namespace Mages.Core.Runtime.Functions
{
    using System;

    /// <summary>
    /// Provide helpers to enable currying.
    /// </summary>
    public static class Curry
    {
        /// <summary>
        /// Checks if the provided args deliver at least one argument.
        /// Otherwise returns null.
        /// </summary>
        /// <param name="function">The function to return or capture.</param>
        /// <param name="args">The args to check and potentially capture.</param>
        /// <returns>A curried function or null.</returns>
        public static Object MinOne(Function function, Object[] args)
        {
            return args.Length < 1 ? function : null;
        }

        /// <summary>
        /// Checks if the provided args deliver at least two arguments.
        /// Otherwise returns null.
        /// </summary>
        /// <param name="function">The function to return or capture.</param>
        /// <param name="args">The args to check and potentially capture.</param>
        /// <returns>A curried function or null.</returns>
        public static Object MinTwo(Function function, Object[] args)
        {
            if (args.Length < 2)
            {
                return args.Length == 0 ? function : rest => function(Recombine2(args, rest));
            }

            return null;
        }

        /// <summary>
        /// Checks if the provided args deliver at least three arguments.
        /// Otherwise returns null.
        /// </summary>
        /// <param name="function">The function to return or capture.</param>
        /// <param name="args">The args to check and potentially capture.</param>
        /// <returns>A curried function or null.</returns>
        public static Object MinThree(Function function, Object[] args)
        {
            if (args.Length < 3)
            {
                return args.Length == 0 ? function : rest => function(RecombineN(args, rest));
            }

            return null;
        }

        /// <summary>
        /// Checks if the provided args deliver at least count argument(s).
        /// Otherwise returns null.
        /// </summary>
        /// <param name="count">The required number of arguments.</param>
        /// <param name="function">The function to return or capture.</param>
        /// <param name="args">The args to check and potentially capture.</param>
        /// <returns>A curried function or null.</returns>
        public static Object Min(Int32 count, Function function, Object[] args)
        {
            if (args.Length < count)
            {
                return args.Length == 0 ? function : rest => function(RecombineN(args, rest));
            }

            return null;
        }

        /// <summary>
        /// Creates a function that shuffles the arguments of a given function
        /// according to the current arguments.
        /// </summary>
        /// <param name="args">The arguments to create the shuffle function.</param>
        /// <returns>The created shuffle function.</returns>
        public static Function Shuffle(Object[] args)
        {
            var end = args.Length - 1;
            var target = args[end] as Function;

            if (target != null)
            {
                var wrapper = target.Target as LocalFunction;
                var parameters = wrapper?.Parameters;

                if (parameters != null)
                {
                    var indices = new Int32[parameters.Length];
                    var result = default(Function);
                    var required = ShuffleParameters(args, parameters, indices);

                    result = new Function(shuffledArgs =>
                    {
                        var length = indices.Length;
                        var normalArgs = new Object[length];
                        var matchedRequired = 0;

                        for (var i = 0; i < length; i++)
                        {
                            var index = indices[i];

                            if (index < shuffledArgs.Length)
                            {
                                normalArgs[i] = shuffledArgs[index];

                                if (parameters[i].IsRequired)
                                {
                                    matchedRequired++;
                                }
                            }
                            else
                            {
                                normalArgs[i] = Undefined.Instance;
                            }
                        }

                        if (matchedRequired < required)
                        {
                            return Curry.Min(length + 1, result, shuffledArgs);
                        }

                        return target.Invoke(normalArgs);
                    });

                    return result;
                }
            }

            return target;
        }

        private static Int32 ShuffleParameters(Object[] args, ParameterDefinition[] parameters, Int32[] indices)
        {
            var start = 0;
            var required = 0;

            for (var i = 0; i < indices.Length; i++)
            {
                indices[i] = i;

                if (parameters[i].IsRequired)
                {
                    required++;
                }
            }

            foreach (var arg in args)
            {
                var s = arg as String;

                if (s != null)
                {
                    for (var j = 0; j < parameters.Length; j++)
                    {
                        if (parameters[j].Name.Equals(s, StringComparison.Ordinal))
                        {
                            for (var i = 0; i < j; i++)
                            {
                                if (indices[i] >= start)
                                {
                                    indices[i]++;
                                }
                            }

                            indices[j] = start++;
                            break;
                        }
                    }
                }
            }

            return required;
        }

        private static Object[] Recombine2(Object[] oldArgs, Object[] newArgs)
        {
            return newArgs.Length > 0 ? new[] { oldArgs[0], newArgs[0] } : oldArgs;
        }

        private static Object[] RecombineN(Object[] oldArgs, Object[] newArgs)
        {
            if (newArgs.Length > 0)
            {
                var args = new Object[oldArgs.Length + newArgs.Length];
                oldArgs.CopyTo(args, 0);
                newArgs.CopyTo(args, oldArgs.Length);
                return args;
            }

            return oldArgs;
        }
    }
}
