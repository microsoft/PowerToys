namespace Mages.Core.Vm.Operations
{
    using Mages.Core.Runtime;
    using Mages.Core.Runtime.Converters;
    using System;

    /// <summary>
    /// Pops two elements from the stack and pushes one.
    /// </summary>
    sealed class RngiOperation : IOperation
    {
        public static readonly IOperation Instance = new RngiOperation();

        private RngiOperation()
        {
        }

        public void Invoke(IExecutionContext context)
        {
            var from = context.Pop().ToNumber();
            var to = context.Pop().ToNumber();
            var result = Range.Create(from, to);
            context.Push(result);
        }

        public override String ToString()
        {
            return "rngi";
        }
    }
}
