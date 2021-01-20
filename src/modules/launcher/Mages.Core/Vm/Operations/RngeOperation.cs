namespace Mages.Core.Vm.Operations
{
    using Mages.Core.Runtime;
    using Mages.Core.Runtime.Converters;
    using System;

    /// <summary>
    /// Pops three elements from the stack and pushes one.
    /// </summary>
    sealed class RngeOperation : IOperation
    {
        public static readonly IOperation Instance = new RngeOperation();

        private RngeOperation()
        {
        }

        public void Invoke(IExecutionContext context)
        {
            var from = context.Pop().ToNumber();
            var to = context.Pop().ToNumber();
            var step = context.Pop().ToNumber();
            var result = Range.Create(from, to, step);
            context.Push(result);
        }

        public override String ToString()
        {
            return "rnge";
        }
    }
}
