namespace Mages.Core.Vm
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Assembles the MAGES runtime memory model.
    /// </summary>
    public sealed class ExecutionContext : IExecutionContext
    {
        private readonly Stack<Object> _stack;
        private readonly IOperation[] _operations;
        private readonly IDictionary<String, Object> _scope;
        private Int32 _position;
        private Boolean _ended;

        /// <summary>
        /// Creates a new execution context.
        /// </summary>
        /// <param name="operations">The operations to use.</param>
        /// <param name="scope">The global scope to use.</param>
        public ExecutionContext(IOperation[] operations, IDictionary<String, Object> scope)
        {
            _stack = new Stack<Object>(64);
            _operations = operations;
            _scope = scope;
            _position = 0;
        }

        /// <summary>
        /// Gets the current position of the execution context.
        /// </summary>
        public Int32 Position
        {
            get { return _position; }
            set { ChangePosition(value); }
        }

        /// <summary>
        /// Gets the position of the last operation.
        /// </summary>
        public Int32 End
        {
            get { return _operations.Length - 1; }
        }

        /// <summary>
        /// Gets the currently used scope of the execution context.
        /// </summary>
        public IDictionary<String, Object> Scope
        {
            get { return _scope; }
        }

        /// <summary>
        /// Executes the operations.
        /// </summary>
        public void Execute()
        {
            while (!_ended && _position < _operations.Length)
            {
                _operations[_position].Invoke(this);
                _position++;
            }
        }

        /// <summary>
        /// Pushes the value onto the stack.
        /// </summary>
        /// <param name="value">The value to push.</param>
        public void Push(Object value)
        {
            _stack.Push(value);
        }

        /// <summary>
        /// Pops a value from the stack.
        /// </summary>
        /// <returns>The last value from the stack.</returns>
        public Object Pop()
        {
            return _stack.Count > 0 ? _stack.Pop() : null;
        }

        private void ChangePosition(Int32 value)
        {
            if (value == Int32.MaxValue)
            {
                _ended = true;
                value = End;
            }

            _position = value;
        }
    }
}
