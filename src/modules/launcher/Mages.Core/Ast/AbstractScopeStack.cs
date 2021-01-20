namespace Mages.Core.Ast
{
    using System.Collections.Generic;

    sealed class AbstractScopeStack
    {
        private readonly Stack<AbstractScope> _scopes;

        public AbstractScopeStack(AbstractScope root)
        {
            _scopes = new Stack<AbstractScope>();
            _scopes.Push(root);
        }

        public AbstractScope Current
        {
            get { return _scopes.Peek(); }
        }

        public void PushNew()
        {
            var scope = new AbstractScope(Current);
            _scopes.Push(scope);
        }

        public AbstractScope PopCurrent()
        {
            return _scopes.Pop();
        }
    }
}
