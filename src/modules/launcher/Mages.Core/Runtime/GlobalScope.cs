namespace Mages.Core.Runtime
{
    using Mages.Core.Runtime.Functions;
    using System;
    using System.Collections.Generic;

    sealed class GlobalScope : BaseScope
    {
        public GlobalScope(IDictionary<String, Object> scope)
            : base(scope ?? new Dictionary<String, Object>(), new Dictionary<String, Object>(Global.Mapping))
        {
        }

        protected override void SetValue(String key, Object value)
        {
            _scope[key] = value;
        }
    }
}
