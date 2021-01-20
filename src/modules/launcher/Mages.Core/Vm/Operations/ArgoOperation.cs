namespace Mages.Core.Vm.Operations
{
    using System;

    /// <summary>
    /// Assigns the argument at index i by popping two values
    /// from the scope and pushing one value back.
    /// </summary>
    sealed class ArgoOperation : IOperation
    {
        private readonly Int32 _index;

        public ArgoOperation(Int32 index)
        {
            _index = index;
        }

        public void Invoke(IExecutionContext context)
        {
            var value = context.Pop();
            var parameters = (Object[])context.Pop();

            if (parameters.Length <= _index)
            {
                var result = new Object[parameters.Length + 1];
                parameters.CopyTo(result, 0);
                result[parameters.Length] = value;
                parameters = result;
            }
            else
            {
                parameters[_index] = value;
            }

            context.Push(parameters);
        }

        public override String ToString()
        {
            return String.Concat("argo ", _index.ToString());
        }
    }
}
