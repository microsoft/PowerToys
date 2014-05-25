using System.Collections.Generic;

namespace Wox.Plugin.SystemPlugins {

	public abstract class BaseSystemPlugin : ISystemPlugin {
		public string PluginDirectory { get; set; }
		public virtual string Name { get { return "System workflow"; } }
		public virtual string Description { get { return "System workflow"; } }
		public virtual string IcoPath { get { return null; } }
		public virtual bool Enabled { get; set; }

		protected abstract List<Result> QueryInternal(Query query);
		protected abstract void InitInternal(PluginInitContext context);



		public List<Result> Query(Query query) {
			if (Enabled) {
				if (string.IsNullOrEmpty(query.RawQuery)) return new List<Result>();
				return QueryInternal(query);
			}
		}

		public void Init(PluginInitContext context) {
			InitInternal(context);
		}


		/// <summary>
		/// Used to save settings
		/// </summary>
		public virtual string PluginId {
			get { return null; }
		}
	}
}