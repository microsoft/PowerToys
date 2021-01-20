namespace Mages.Core.Vm.Operations
{
    using System;

    /// <summary>
    /// Tries to remove the named variable from the scope and pushes the result
    /// on the stack.
    /// </summary>
    sealed class DelVarOperation : IOperation
    {
        private readonly String _name;

        public DelVarOperation(String name)
        {
            _name = name;
        }

        public void Invoke(IExecutionContext context)
        {
            var result = context.Scope.Remove(_name);
            context.Push(result);
        }

        public override String ToString()
        {
            return "delvar " + _name;
        }
    }
}
