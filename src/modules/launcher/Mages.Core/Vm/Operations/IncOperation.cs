namespace Mages.Core.Vm.Operations
{
    using Mages.Core.Runtime;
    using System;

    /// <summary>
    /// Pops one element from the stack and pushes one element.
    /// </summary>
    sealed class IncOperation : IOperation
    {
        public static readonly IOperation Instance = new IncOperation();

        private IncOperation()
        {
        }

        public void Invoke(IExecutionContext context)
        {
            var value = context.Pop();
            var arguments = new Object[] { 1.0, value };
            context.Push(BinaryOperators.Add(arguments));
        }

        public override String ToString()
        {
            return "inc";
        }
    }
}
