namespace Mages.Core.Vm
{
    using System;

    /// <summary>
    /// A set of handy helpers for operations.
    /// </summary>
    public static class OperationExtensions
    {
        /// <summary>
        /// Serializes the given operations to a string of instructions.
        /// </summary>
        /// <param name="operations">The operations to serialize.</param>
        /// <returns>The string with the instructions.</returns>
        public static String Serialize(this IOperation[] operations)
        {
            var instructions = new String[operations.Length];

            for (var i = 0; i < operations.Length; i++)
            {
                instructions[i] = operations[i].ToString();
            }

            return String.Join(Environment.NewLine, instructions);
        }
    }
}
