namespace Mages.Core.Vm.Operations
{
    using Mages.Core.Runtime.Converters;
    using System;

    /// <summary>
    /// Pops three elements from the stack and pushes one.
    /// </summary>
    sealed class CondOperation : IOperation
    {
        public static readonly IOperation Instance = new CondOperation();

        private CondOperation()
        {
        }

        public void Invoke(IExecutionContext context)
        {
            var condition = context.Pop().ToBoolean();
            var primary = context.Pop();
            var alternative = context.Pop();
            context.Push(condition ? primary : alternative);
        }

        public override String ToString()
        {
            return "cond";
        }
    }
}
