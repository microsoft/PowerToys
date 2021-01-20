namespace Mages.Core.Vm.Operations
{
    using System;

    /// <summary>
    /// Checks if the parameters |Object[]| is less / equal than i by
    /// popping one value from the stack and pushing back two values.
    /// </summary>
    sealed class ArgcOperation : IOperation
    {
        private readonly Int32 _index;

        public ArgcOperation(Int32 index)
        {
            _index = index;
        }

        public void Invoke(IExecutionContext context)
        {
            var parameters = (Object[])context.Pop();
            context.Push(parameters);
            context.Push(parameters.Length <= _index || parameters[_index].IsUndefined());
        }

        public override String ToString()
        {
            return String.Concat("argc ", _index.ToString());
        }
    }
}
