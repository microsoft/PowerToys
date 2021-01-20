namespace Mages.Core.Vm.Operations
{
    using System;

    /// <summary>
    /// Pops one element from the stack.
    /// </summary>
    sealed class PopOperation : IOperation
    {
        public static readonly IOperation Instance = new PopOperation();

        private PopOperation()
        {
        }

        public void Invoke(IExecutionContext context)
        {
            context.Pop();
        }

        public override String ToString()
        {
            return "pop";
        }
    }
}
