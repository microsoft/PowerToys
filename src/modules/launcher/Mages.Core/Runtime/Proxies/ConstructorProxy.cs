namespace Mages.Core.Runtime.Proxies
{
    using System;
    using System.Linq;
    using System.Reflection;

    sealed class ConstructorProxy : FunctionProxy
    {
        public ConstructorProxy(WrapperObject obj, ConstructorInfo[] ctors)
            : base(obj, ctors)
        {
            _proxy = new Function(Invoke);
        }

        private Object Invoke(Object[] arguments)
        {
            var types = arguments.Select(m => m != null ? m.GetType() : typeof(Object)).ToArray();
            var ctor = _methods.Find(types, ref arguments) as ConstructorInfo;

            if (ctor != null)
            {
                return ctor.Call(_obj, arguments);
            }

            return TryCurry(arguments);
        }
    }
}
