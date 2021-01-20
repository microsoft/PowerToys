namespace Mages.Core.Vm.Operations
{
    using Mages.Core.Runtime;
    using Mages.Core.Runtime.Converters;
    using System;

    /// <summary>
    /// Peeks the top element from the stack.
    /// </summary>
    sealed class InitMatOperation : IOperation
    {
        private readonly Int32 _row;
        private readonly Int32 _col;

        public InitMatOperation(Int32 row, Int32 col)
        {
            _row = row;
            _col = col;
        }

        public void Invoke(IExecutionContext context)
        {
            var value = context.Pop().ToNumber();
            var matrix = (Double[,])context.Pop();
            matrix.SetValue(_row, _col, value);
            context.Push(matrix);
        }

        public override String ToString()
        {
            return String.Concat("initmat ", _row.ToString(), " ", _col.ToString());
        }
    }
}
