using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FancyZonesEditor
{
	public class MonitorVM : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		public MonitorVM()
		{
			AddCommand = new RelayCommand(AddCommandExecute, AddCommandCanExecute);
			DeleteCommand = new RelayCommand(DeleteCommandExecute, DeleteCommandCanExecute);

			Monitors = new ObservableCollection<MonitorInfo>();
			Monitors.Add(new MonitorInfo(0, "Monitor 1", 100, 100));
			Monitors.Add(new MonitorInfo(1, "Monitor 2", 100, 200));
			Monitors.Add(new MonitorInfo(2, "Monitor 3", 100, 300));
		}

		#region Properties

		private ObservableCollection<MonitorInfo> monitors;
		public ObservableCollection<MonitorInfo> Monitors
		{
			get { return monitors; }
			set { monitors = value; }
		}

		private int height = 100;

		public int Height
		{
			get { return height; }
			set
			{
				height = value;
				PropertyChanged(this, new PropertyChangedEventArgs("Height"));
				AddCommand.RaiseCanExecuteChanged();
			}
		}

		private int width = 100;

		public int Width
		{
			get { return width; }
			set
			{
				width = value;
				PropertyChanged(this, new PropertyChangedEventArgs("Width"));
			}
		}


		#endregion Properties


		#region Commands

		private RelayCommand addCommand;
		public RelayCommand AddCommand
		{
			get => addCommand;
			set => addCommand = value;

		}

		private bool AddCommandCanExecute(object var)
		{
			if (Height > 0 && Width > 0) return true;
			return false;
		}

		private void AddCommandExecute(object var)
		{
			Monitors.Add(new MonitorInfo(Monitors.Count, "Monitor " + Monitors.Count + 1, Height, Width));
		}


		private ICommand deleteCommand;
		public ICommand DeleteCommand
		{
			get => deleteCommand;
			set => deleteCommand = value;

		}

		private bool DeleteCommandCanExecute(object var)
		{
			return true;
		}

		private void DeleteCommandExecute(object var)
		{
			Monitors.Remove(Monitors.Last<MonitorInfo>());
		}

		#endregion Commands

	}

	public class MonitorInfo
	{
		public MonitorInfo(int id, string name, int height, int width)
		{
			Id = id;
			Name = name;
			Height = height;
			Width = width;
		}

		private int id;

		public int Id
		{
			get { return id; }
			set { id = value; }
		}

		private string name;

		public string Name
		{
			get { return name; }
			set { name = value; }
		}

		private int height;

		public int Height
		{
			get { return height; }
			set { height = value; }
		}

		private int width;

		public int Width
		{
			get { return width; }
			set { width = value; }
		}

	}
}
