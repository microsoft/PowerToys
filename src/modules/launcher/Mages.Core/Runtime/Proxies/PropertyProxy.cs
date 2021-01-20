namespace Mages.Core.Runtime.Proxies
{
    using System;
    using System.Reflection;

    sealed class PropertyProxy : BaseProxy
    {
        private readonly PropertyInfo _property;

        public PropertyProxy(WrapperObject obj, PropertyInfo property)
            : base(obj)
        {
            _property = property;
        }

        protected override Object GetValue()
        {
            if (_property.CanRead)
            {
                var target = _obj.Content;

                try { return _property.GetValue(target, null); }
                catch { return null; }
            }

            return null;
        }

        protected override void SetValue(Object value)
        {
            if (_property.CanWrite)
            {
                var target = _obj.Content;
                var result = Convert(value, _property.PropertyType);

                try { _property.SetValue(target, result, null); }
                catch { }
            }
        }
    }
}
