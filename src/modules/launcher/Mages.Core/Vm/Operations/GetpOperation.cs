namespace Mages.Core.Vm.Operations
{
    using Mages.Core.Runtime.Functions;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Takes two elements from the stack and pushes the result.
    /// </summary>
    sealed class GetpOperation : IOperation
    {
        public static readonly IOperation Instance = new GetpOperation();

        private GetpOperation()
        {
        }

        public void Invoke(IExecutionContext context)
        {
            var instance = context.Pop();
            var name = context.Pop() as String;
            var obj = instance as IDictionary<String, Object>;
            var result = default(Object);

            if (name != null && instance != null)
            {
                if (obj == null || !obj.TryGetValue(name, out result))
                {
                    AttachedProperties.TryFind(instance, name, out result);
                }
            }
            
            context.Push(result);
        }

        public override String ToString()
        {
            return "getp";
        }
    }
}
