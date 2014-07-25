using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Wox.Plugin.SystemPlugins.Sys {
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
