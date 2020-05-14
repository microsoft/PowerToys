using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MenusWPF.Models
{
    public class MenuItem
    {
		public MenuItem(Guid id, string title, string name, bool isChecked, ICommand command, String sliderVisible)
		{
			Id = id;
			Title = title;
			Name = name;
			IsChecked = isChecked;
			Command = command;
			SliderVisible = "Hidden";

		}

		public MenuItem(Guid id, string title, string name, bool isChecked, ICommand command, int sliderValue)
		{
			Id = id;
			Title = title;
			Name = name;
			IsChecked = isChecked;
			Command = command;
			SliderVisible = "Visible";
			SliderValue = sliderValue;


		}

		private Guid guid;

		public Guid Id
		{
			get { return guid; }
			set { guid = value; }
		}

		private string title;

		public string Title
		{
			get { return title; }
			set { title = value; }
		}

		private string  name;

		public string  Name
		{
			get { return name; }
			set { name = value; }
		}

		private bool isChecked;

		public bool IsChecked
		{
			get { return isChecked; }
			set { isChecked = value; }
		}

		private ObservableCollection<MenuItem> subMenuItems;

		public ObservableCollection<MenuItem> SubMenuItems
		{
			get { return subMenuItems; }
			set { subMenuItems = value; }
		}


		private ICommand command;

		public ICommand Command
		{
			get { return command; }
			set { command = value; }
		}

		private String sliderVisible;

		public String SliderVisible
		{
			get { return sliderVisible; }
			set { sliderVisible = value; } 
		}

		private int sliderValue;

		public int SliderValue
		{
			get { return sliderValue; }
			set { sliderValue = value; }
		}



	}
}
