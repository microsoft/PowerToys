namespace Mages.Core.Runtime.Proxies
{
    using Mages.Core.Runtime.Functions;
    using System;
    using System.Reflection;

    abstract class FunctionProxy : BaseProxy
    {
        protected readonly MethodBase[] _methods;
        private readonly Int32 _maxParameters;
        protected Function _proxy;

        public FunctionProxy(WrapperObject obj, MethodBase[] methods)
            : base(obj)
        {
            _methods = methods;
            _maxParameters = methods.MaxParameters();
        }

        protected Object TryCurry(Object[] arguments)
        {
            return Curry.Min(_maxParameters, _proxy, arguments);
        }

        protected override Object GetValue()
        {
            return _proxy;
        }

        protected override void SetValue(Object value)
        {
        }
    }
}
