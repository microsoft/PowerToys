namespace Mages.Core.Vm.Operations
{
    using Mages.Core.Runtime.Functions;
    using System;

    /// <summary>
    /// Pops at least two elements from the stack and pushes one.
    /// </summary>
    sealed class SetcOperation : IOperation
    {
        private readonly Int32 _length;

        public SetcOperation(Int32 length)
        {
            _length = length;
        }

        public void Invoke(IExecutionContext context)
        {
            var value = context.Pop();
            var obj = context.Pop();
            var function = default(Procedure);
            var arguments = new Object[_length];

            for (var i = 0; i < arguments.Length; i++)
            {
                arguments[i] = context.Pop();
            }

            if (obj != null && TypeProcedures.TryFind(obj, out function))
            {
                function.Invoke(arguments, value);
            }

            context.Push(value);
        }

        public override String ToString()
        {
            return String.Concat("setc ", _length.ToString());
        }
    }
}
