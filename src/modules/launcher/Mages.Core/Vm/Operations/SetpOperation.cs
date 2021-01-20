namespace Mages.Core.Vm.Operations
{
    using Mages.Core.Runtime;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Pops three elements from the stack and pushes one.
    /// </summary>
    sealed class SetpOperation : IOperation
    {
        public static readonly IOperation Instance = new SetpOperation();

        private SetpOperation()
        {
        }

        public void Invoke(IExecutionContext context)
        {
            var value = context.Pop();
            var obj = context.Pop() as IDictionary<String, Object>;
            var name = context.Pop() as String;

            if (obj != null && name != null)
            {
                obj.SetProperty(name, value);
            }

            context.Push(value);
        }

        public override String ToString()
        {
            return "setp";
        }
    }
}
