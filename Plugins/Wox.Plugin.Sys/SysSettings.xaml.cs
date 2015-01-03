using System.Collections.Generic;
using System.Windows.Controls;

namespace Wox.Plugin.Sys {
	/// <summary>
	/// Interaction logic for SysSettings.xaml
	/// </summary>
	public partial class SysSettings : UserControl {
		public SysSettings(List<Result> Results) {
			InitializeComponent();

			foreach (var Result in Results) {
				this.lbxCommands.Items.Add(Result);
			}
		}
	}
}
