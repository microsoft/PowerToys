using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins
{

    public abstract class BaseSystemPlugin : ISystemPlugin
    {
        public string PluginDirectory { get; set; }

        public abstract string ID { get; }
        public virtual string Name { get { return "System workflow"; } }
        public virtual string Description { get { return "System workflow"; } }
        public virtual string IcoPath { get { return null; } }

        public string FullIcoPath
        {
            get
            {
                if (string.IsNullOrEmpty(IcoPath)) return string.Empty;
                if (IcoPath.StartsWith("data:"))
                {
                    return IcoPath;
                }

                return Path.Combine(PluginDirectory, IcoPath);
            }
        }

        protected abstract List<Result> QueryInternal(Query query);

        protected abstract void InitInternal(PluginInitContext context);

        public List<Result> Query(Query query)
        {
            if (string.IsNullOrEmpty(query.RawQuery)) return new List<Result>();
            var customizedPluginConfig = UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == ID);
            if (customizedPluginConfig != null && customizedPluginConfig.Disabled)
            {
                return new List<Result>();
            }
            return QueryInternal(query);
        }

        public void Init(PluginInitContext context)
        {
            InitInternal(context);
        }
    }
}