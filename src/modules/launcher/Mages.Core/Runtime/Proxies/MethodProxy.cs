namespace Mages.Core.Runtime.Proxies
{
    using System;
    using System.Linq;
    using System.Reflection;

    sealed class MethodProxy : FunctionProxy
    {
        public MethodProxy(WrapperObject obj, MethodInfo[] methods)
            : base(obj, methods)
        {
            _proxy = new Function(Invoke);
        }

        private Object Invoke(Object[] arguments)
        {
            var parameters = arguments.Select(m => m != null ? m.GetType() : typeof(Object)).ToArray();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.OptionalParamBinding | BindingFlags.InvokeMethod;
            var method = Type.DefaultBinder.SelectMethod(flags, _methods, parameters, null) ?? _methods.Find(parameters, ref arguments);

            if (method != null)
            {
                return method.Call(_obj, arguments);
            }

            return TryCurry(arguments);
        }
    }
}
