using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.System
{
    public abstract class BaseSystemPlugin :ISystemPlugin
    {
        protected abstract List<Result> QueryInternal(Query query);
        protected abstract void InitInternal(PluginInitContext context);

        public List<Result> Query(Query query)
        {
            if (string.IsNullOrEmpty(query.RawQuery)) return new List<Result>();
            return QueryInternal(query);
        }

        public void Init(PluginInitContext context)
        {
            InitInternal(context);
        }

        public string Name
        {
            get
            {
                return "System workflow";
            }
        }

        public string Description
        {
            get
            {
                return "System workflow";
            }
        }
    }
}
