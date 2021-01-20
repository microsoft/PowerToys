namespace Mages.Core.Vm.Operations
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Pops one value and tries to remove the key from the object.
    /// Pushes the result on the stack.
    /// </summary>
    sealed class DelKeyOperation : IOperation
    {
        private readonly String _name;

        public DelKeyOperation(String name)
        {
            _name = name;
        }

        public void Invoke(IExecutionContext context)
        {
            var obj = context.Pop() as IDictionary<String, Object>;
            var result = false;

            if (obj != null)
            {
                result = obj.Remove(_name);
            }

            context.Push(result);
        }

        public override String ToString()
        {
            return "delkey " + _name;
        }
    }
}
