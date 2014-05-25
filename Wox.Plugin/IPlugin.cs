using System.Collections.Generic;

namespace Wox.Plugin {
	public interface IPlugin {
		List<Result> Query(Query query);
		void Init(PluginInitContext context);

		/// <summary>
		/// Used when saving Plug-in settings
		/// </summary>
		string PluginId { get; }
	}
}