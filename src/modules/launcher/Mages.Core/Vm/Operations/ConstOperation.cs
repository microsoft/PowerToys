namespace Mages.Core.Vm.Operations
{
    using Mages.Core.Runtime;
    using System;

    /// <summary>
    /// Pushes a constant value on the stack.
    /// </summary>
    sealed class ConstOperation : IOperation
    {
        private readonly Object _constant;

        /// <summary>
        /// Contains a const operation pushing null on the stack.
        /// </summary>
        public static readonly IOperation Null = new ConstOperation(null);

        public ConstOperation(Object constant)
        {
            _constant = constant;
        }

        public void Invoke(IExecutionContext context)
        {
            context.Push(_constant);
        }

        public override String ToString()
        {
            return String.Concat("const ", _constant != null ? _constant.GetHashCode() : 0);
        }
    }
}
