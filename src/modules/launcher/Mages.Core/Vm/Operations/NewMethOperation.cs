namespace Mages.Core.Vm.Operations
{
    using Mages.Core.Runtime;
    using System;

    /// <summary>
    /// Pops two elements from the stack and pushes three new element on the stack.
    /// </summary>
    sealed class NewMethOperation : IOperation
    {
        private readonly ParameterDefinition[] _parameters;
        private readonly IOperation[] _operations;

        public NewMethOperation(ParameterDefinition[] parameters,IOperation[] operations)
        {
            _parameters = parameters;
            _operations = operations;
        }

        public void Invoke(IExecutionContext context)
        {
            var name = context.Pop();
            var obj = context.Pop();
            var parentScope = context.Scope;
            var function = new LocalFunction(obj, parentScope, _parameters, _operations);
            context.Push(obj);
            context.Push(name);
            context.Push(function.Pointer);
        }

        public override String ToString()
        {
            var instructions = new String[3];
            instructions[0] = "newmeth start";
            instructions[1] = _operations.Serialize();
            instructions[2] = "newmeth end";
            return String.Join(Environment.NewLine, instructions);
        }
    }
}
