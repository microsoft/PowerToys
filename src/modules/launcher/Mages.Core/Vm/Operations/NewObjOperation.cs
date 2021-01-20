namespace Mages.Core.Vm.Operations
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Pushes one new element on the stack.
    /// </summary>
    sealed class NewObjOperation : IOperation
    {
        public static readonly IOperation Instance = new NewObjOperation();

        private NewObjOperation()
        {
        }

        public void Invoke(IExecutionContext context)
        {
            context.Push(new Dictionary<String, Object>());
        }

        public override String ToString()
        {
            return "newobj";
        }
    }
}
