namespace Mages.Core.Vm.Operations
{
    using Mages.Core.Runtime;
    using System;

    /// <summary>
    /// Peeks the top element from the stack.
    /// </summary>
    sealed class SetsOperation : IOperation
    {
        private readonly String _name;

        public SetsOperation(String name)
        {
            _name = name;
        }

        public void Invoke(IExecutionContext context)
        {
            var value = context.Pop();
            context.Scope.SetProperty(_name, value);
            context.Push(value);
        }

        public override String ToString()
        {
            return String.Concat("sets ", _name);
        }
    }
}
