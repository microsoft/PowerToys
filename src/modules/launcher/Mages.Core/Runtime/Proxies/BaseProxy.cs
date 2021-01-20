namespace Mages.Core.Runtime.Proxies
{
    using Mages.Core.Runtime.Converters;
    using System;

    abstract class BaseProxy
    {
        protected readonly WrapperObject _obj;

        public BaseProxy(WrapperObject obj)
        {
            _obj = obj;
        }

        public Object Value
        {
            get { return Convert(GetValue()); }
            set { SetValue(value); }
        }

        protected abstract void SetValue(Object value);

        protected abstract Object GetValue();

        protected Object Convert(Object value, Type target)
        {
            var source = value != null ? value.GetType() : target;
            return source.Convert(value, target);
        }

        private Object Convert(Object value)
        {
            if (Object.ReferenceEquals(value, _obj.Content))
            {
                return _obj;
            }
            else if (value != null)
            {
                var source = value.GetType();
                var target = source.FindPrimitive();
                return source.Convert(value, target);
            }

            return null;
        }
    }
}
