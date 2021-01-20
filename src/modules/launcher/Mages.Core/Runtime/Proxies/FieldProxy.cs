namespace Mages.Core.Runtime.Proxies
{
    using System;
    using System.Reflection;

    sealed class FieldProxy : BaseProxy
    {
        private readonly FieldInfo _field;

        public FieldProxy(WrapperObject obj, FieldInfo field)
            : base(obj)
        {
            _field = field;
        }

        protected override Object GetValue()
        {
            var target = _obj.Content;
            return _field.GetValue(target);
        }

        protected override void SetValue(Object value)
        {
            var target = _obj.Content;
            var result = Convert(value, _field.FieldType);

            try { _field.SetValue(target, result); } 
            catch { }
        }
    }
}
