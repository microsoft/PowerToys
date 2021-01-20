namespace Mages.Core.Runtime.Converters
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    sealed class TargetWrapper
    {
        private readonly Object _target;

        public TargetWrapper(Object target)
        {
            _target = target;
        }

        public static Type Construct(Type returnType, ParameterInfo[] parameters)
        {
            var types = new List<Type>();

            foreach (var parameter in parameters)
            {
                types.Add(parameter.ParameterType);
            }
            
            if (returnType != typeof(void))
            {
                types.Add(returnType);
            }

            if (types.Count == 0)
            {
                return typeof(TargetWrapper);
            }

            var type = GetType(parameters.Length, types.Count);
            return type?.MakeGenericType(types.ToArray());
        }

        private static Type GetType(Int32 parameters, Int32 types)
        {
            if (types > parameters)
            {
                switch (parameters)
                {
                    case 0: return typeof(FuncTargetWrapper<>);
                    case 1: return typeof(FuncTargetWrapper<,>);
                    case 2: return typeof(FuncTargetWrapper<,,>);
                    case 3: return typeof(FuncTargetWrapper<,,,>);
                    case 4: return typeof(FuncTargetWrapper<,,,,>);
                    case 5: return typeof(FuncTargetWrapper<,,,,,>);
                }
            }
            else
            {
                switch (parameters)
                {
                    case 1: return typeof(ActionTargetWrapper<>);
                    case 2: return typeof(ActionTargetWrapper<,>);
                    case 3: return typeof(ActionTargetWrapper<,,>);
                    case 4: return typeof(ActionTargetWrapper<,,,>);
                    case 5: return typeof(ActionTargetWrapper<,,,,>);
                }
            }

            return null;
        }

        public void Invoke()
        {
            var f = _target as Function;
            f?.Call();
        }
    }

    sealed class ActionTargetWrapper<TFrom>
    {
        private readonly Object _target;

        public ActionTargetWrapper(Object target)
        {
            _target = target;
        }

        public void Invoke(TFrom arg)
        {
            var f = _target as Function;
            f?.Call(arg);
        }
    }

    sealed class ActionTargetWrapper<TFrom1, TFrom2>
    {
        private readonly Object _target;

        public ActionTargetWrapper(Object target)
        {
            _target = target;
        }

        public void Invoke(TFrom1 arg1, TFrom2 arg2)
        {
            var f = _target as Function;
            f?.Call(arg1, arg2);
        }
    }

    sealed class ActionTargetWrapper<TFrom1, TFrom2, TFrom3>
    {
        private readonly Object _target;

        public ActionTargetWrapper(Object target)
        {
            _target = target;
        }

        public void Invoke(TFrom1 arg1, TFrom2 arg2, TFrom3 arg3)
        {
            var f = _target as Function;
            f?.Call(arg1, arg2, arg3);
        }
    }

    sealed class ActionTargetWrapper<TFrom1, TFrom2, TFrom3, TFrom4>
    {
        private readonly Object _target;

        public ActionTargetWrapper(Object target)
        {
            _target = target;
        }

        public void Invoke(TFrom1 arg1, TFrom2 arg2, TFrom3 arg3, TFrom4 arg4)
        {
            var f = _target as Function;
            f?.Call(arg1, arg2, arg3, arg4);
        }
    }

    sealed class ActionTargetWrapper<TFrom1, TFrom2, TFrom3, TFrom4, TFrom5>
    {
        private readonly Object _target;

        public ActionTargetWrapper(Object target)
        {
            _target = target;
        }

        public void Invoke(TFrom1 arg1, TFrom2 arg2, TFrom3 arg3, TFrom4 arg4, TFrom5 arg5)
        {
            var f = _target as Function;
            f?.Call(arg1, arg2, arg3, arg4, arg5);
        }
    }

    sealed class FuncTargetWrapper<TTo>
    {
        private readonly Object _target;

        public FuncTargetWrapper(Object target)
        {
            _target = target;
        }

        public TTo Invoke()
        {
            var f = _target as Function;
            var r = f?.Call() ?? _target;
            var c = Helpers.Converters.FindConverter(r.GetType(), typeof(TTo));
            return (TTo)c.Invoke(r);
        }
    }

    sealed class FuncTargetWrapper<TFrom, TTo>
    {
        private readonly Object _target;

        public FuncTargetWrapper(Object target)
        {
            _target = target;
        }

        public TTo Invoke(TFrom arg)
        {
            var f = _target as Function;
            var r = f?.Call(arg) ?? _target;
            var c = Helpers.Converters.FindConverter(r.GetType(), typeof(TTo));
            return (TTo)c.Invoke(r);
        }
    }

    sealed class FuncTargetWrapper<TFrom1, TFrom2, TTo>
    {
        private readonly Object _target;

        public FuncTargetWrapper(Object target)
        {
            _target = target;
        }

        public TTo Invoke(TFrom1 arg1, TFrom2 arg2)
        {
            var f = _target as Function;
            var r = f?.Call(arg1, arg2) ?? _target;
            var c = Helpers.Converters.FindConverter(r.GetType(), typeof(TTo));
            return (TTo)c.Invoke(r);
        }
    }

    sealed class FuncTargetWrapper<TFrom1, TFrom2, TFrom3, TTo>
    {
        private readonly Object _target;

        public FuncTargetWrapper(Object target)
        {
            _target = target;
        }

        public TTo Invoke(TFrom1 arg1, TFrom2 arg2, TFrom3 arg3)
        {
            var f = _target as Function;
            var r = f?.Call(arg1, arg2, arg3) ?? _target;
            var c = Helpers.Converters.FindConverter(r.GetType(), typeof(TTo));
            return (TTo)c.Invoke(r);
        }
    }

    sealed class FuncTargetWrapper<TFrom1, TFrom2, TFrom3, TFrom4, TTo>
    {
        private readonly Object _target;

        public FuncTargetWrapper(Object target)
        {
            _target = target;
        }

        public TTo Invoke(TFrom1 arg1, TFrom2 arg2, TFrom3 arg3, TFrom4 arg4)
        {
            var f = _target as Function;
            var r = f?.Call(arg1, arg2, arg3, arg4) ?? _target;
            var c = Helpers.Converters.FindConverter(r.GetType(), typeof(TTo));
            return (TTo)c.Invoke(r);
        }
    }

    sealed class FuncTargetWrapper<TFrom1, TFrom2, TFrom3, TFrom4, TFrom5, TTo>
    {
        private readonly Object _target;

        public FuncTargetWrapper(Object target)
        {
            _target = target;
        }

        public TTo Invoke(TFrom1 arg1, TFrom2 arg2, TFrom3 arg3, TFrom4 arg4, TFrom5 arg5)
        {
            var f = _target as Function;
            var r = f?.Call(arg1, arg2, arg3, arg4, arg5) ?? _target;
            var c = Helpers.Converters.FindConverter(r.GetType(), typeof(TTo));
            return (TTo)c.Invoke(r);
        }
    }
}
