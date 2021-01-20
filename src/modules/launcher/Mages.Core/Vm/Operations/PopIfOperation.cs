namespace Mages.Core.Vm.Operations
{
    using Mages.Core.Runtime.Converters;
    using System;

    /// <summary>
    /// Pops one element from the stack.
    /// </summary>
    sealed class PopIfOperation : IOperation
    {
        public static readonly IOperation Instance = new PopIfOperation();

        private PopIfOperation()
        {
        }

        public void Invoke(IExecutionContext context)
        {
            var shouldSkip = context.Pop().ToBoolean();

            if (shouldSkip)
            {
                context.Position++;
            }
        }

        public override String ToString()
        {
            return "popif";
        }
    }
}
