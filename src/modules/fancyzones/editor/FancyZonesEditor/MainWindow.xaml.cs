using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FancyZonesEditor.Models;
using MahApps.Metro.Controls;

namespace FancyZonesEditor
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : MetroWindow
	{
		#region Properties

		private static string c_defaultNamePrefix = "Custom Layout ";
		private bool _editing = false;
		private Settings _settings = ((App)Application.Current).ZoneSettings;

		#endregion

		#region Constructors

		public MainWindow()
		{
			InitializeComponent();
			DataContext = _settings;
		}

		#endregion

		#region Methods

		private void DecrementZoneCountButton_Click(object sender, RoutedEventArgs e)
		{
			if (_settings.ZoneCount > 1)
			{
				_settings.ZoneCount--;
			}
		}

		private void IncrementZoneCountButton_Click(object sender, RoutedEventArgs e)
		{
			if (_settings.ZoneCount < 40)
			{
				_settings.ZoneCount++;
			}
		}

		private void NewCustomLayoutButton_Click(object sender, RoutedEventArgs e)
		{
			WindowLayout window = new WindowLayout();

			window.Show();
			this.Close();
		}

		private void LayoutItem_Click(object sender, MouseButtonEventArgs e)
		{
			Select(((Border)sender).DataContext as LayoutModel);
			EditSelectedLayoutButton.IsEnabled = true;
		}

		private void EditLayoutButton_Click(object sender, RoutedEventArgs e)
		{
			EditorOverlay mainEditor = EditorOverlay.Current;
			LayoutModel model = mainEditor.DataContext as LayoutModel;
			EditorWindow window;

			if (model == null)
			{
				return;
			}

			_editing = true;
			this.Close();

			bool isPredefinedLayout = Settings.IsPredefinedLayout(model);
			model.IsSelected = false;

			if (!_settings.CustomModels.Contains(model) || isPredefinedLayout)
			{
				if (isPredefinedLayout)
				{
					// make a copy
					model = model.Clone();
					mainEditor.DataContext = model;
				}

				int maxCustomIndex = 0;
				foreach (LayoutModel customModel in _settings.CustomModels)
				{
					string name = customModel.Name;
					if (name.StartsWith(c_defaultNamePrefix))
					{
						int i;
						if (Int32.TryParse(name.Substring(c_defaultNamePrefix.Length), out i))
						{
							if (maxCustomIndex < i)
							{
								maxCustomIndex = i;
							}
						}
					}
				}

				model.Name = c_defaultNamePrefix + (++maxCustomIndex);
			}

			mainEditor.Edit();

			if (model is GridLayoutModel)
			{
				window = new GridEditorWindow();
			}
			else
			{
				window = new CanvasEditorWindow();
			}

			window.Owner = EditorOverlay.Current;
			window.DataContext = model;

			window.Show();
		}

		private void ApplyButton_Click(object sender, RoutedEventArgs e)
		{
			EditorOverlay mainEditor = EditorOverlay.Current;
			LayoutModel model = mainEditor.DataContext as LayoutModel;

			if (model != null)
			{
				if (model is GridLayoutModel)
				{
					model.Apply(mainEditor.GetZoneRects());
				}
				else
				{
					model.Apply((model as CanvasLayoutModel).Zones.ToArray());
				}
			}

			this.Close();
		}

		private void OnClosed(object sender, EventArgs e)
		{
			if (!_editing)
			{
				EditorOverlay.Current.Close();
			}
		}

		private void OnDelete(object sender, RoutedEventArgs e)
		{
			LayoutModel model = ((FrameworkElement)sender).DataContext as LayoutModel;

			if (model.IsSelected)
			{
				SetSelectedItem();
			}

			model.Delete();
		}

		private void OnSizeChanged(object sender, SizeChangedEventArgs e)
		{
			var width = e.NewSize.Width;

			if (width < 900)
			{
				Footer.Orientation = Orientation.Vertical;
				FooterSplitter.Visibility = Visibility.Collapsed;

				if (width < 550)
				{
					Footer.HorizontalAlignment = HorizontalAlignment.Center;
					SpacingPropertySettings.Orientation = Orientation.Vertical;
					SpacingPropertySettings.HorizontalAlignment = HorizontalAlignment.Center;
				}
				else
				{
					Footer.HorizontalAlignment = HorizontalAlignment.Left;
					SpacingPropertySettings.Orientation = Orientation.Horizontal;
					SpacingPropertySettings.HorizontalAlignment = HorizontalAlignment.Left;
				}
			}
			else
			{
				SpacingPropertySettings.Orientation = Orientation.Horizontal;
				SpacingPropertySettings.HorizontalAlignment = HorizontalAlignment.Left;
				Footer.Orientation = Orientation.Horizontal;
				Footer.HorizontalAlignment = HorizontalAlignment.Left;
				FooterSplitter.Visibility = Visibility.Visible;
			}
		}

		private void Select(LayoutModel newSelection)
		{
			LayoutModel currentSelection = EditorOverlay.Current.DataContext as LayoutModel;

			if (currentSelection != null)
			{
				currentSelection.IsSelected = false;
			}

			newSelection.IsSelected = true;
			EditorOverlay.Current.DataContext = newSelection;
		}

		private void InitializedEventHandler(object sender, EventArgs e)
		{
			SetSelectedItem();
		}

		#region Helpers

		private void SetSelectedItem()
		{
			foreach (LayoutModel model in _settings.CustomModels)
			{
				if (model.IsSelected)
				{
					TemplateTab.SelectedItem = model;
					EditSelectedLayoutButton.IsEnabled = true;
				}
			}
		}

		#endregion

		#endregion
	}
}
