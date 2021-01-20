namespace Mages.Core.Vm.Operations
{
    using Mages.Core.Runtime.Functions;
    using System;

    /// <summary>
    /// Takes two elements from the stack and pushes one.
    /// </summary>
    sealed class GetcOperation : IOperation
    {
        private readonly Int32 _length;

        public GetcOperation(Int32 length)
        {
            _length = length;
        }

        public void Invoke(IExecutionContext context)
        {
            var result = default(Object);
            var obj = context.Pop();
            var arguments = new Object[_length];

            for (var i = 0; i < arguments.Length; i++)
            {
                arguments[i] = context.Pop();
            }

            if (obj != null)
            {
                var function = obj as Function;

                if (function != null || TypeFunctions.TryFind(obj, out function))
                {
                    result = function.Invoke(arguments);
                }
            }

            context.Push(result);
        }

        public override String ToString()
        {
            return String.Concat("getc ", _length.ToString());
        }
    }
}
