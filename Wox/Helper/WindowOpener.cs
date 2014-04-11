using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace Wox.Helper
{
	public static class WindowOpener
	{
		public static T Open<T>(params object[] args) where T : Window
		{
			var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.GetType() == typeof(T))
						 ?? (T)Activator.CreateInstance(typeof(T), args);
			window.Show();
			window.Focus();
			Application.Current.MainWindow.Hide();
			
			return (T)window;
		}
	}
}