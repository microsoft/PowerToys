namespace Mages.Core.Vm.Operations
{
    using System;

    /// <summary>
    /// Stops the execution without changing the stack.
    /// </summary>
    sealed class RetOperation : IOperation
    {
        public static readonly IOperation Instance = new RetOperation();

        private RetOperation()
        {
        }

        public void Invoke(IExecutionContext context)
        {
            context.Pause();
        }

        public override String ToString()
        {
            return "ret";
        }
    }
}
