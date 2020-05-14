using MenusWPF.Helpers.WpfApp1;
using MenusWPF.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace MenusWPF.ViewModels
{
    public class MainWindowVM : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public MainWindowVM()
        {
            AddCommand = new RelayCommand<MenuItem>(AddCommandExecute, AddCommandCanExecute);

            menuItems = new ObservableCollection<MenuItem>();
            MenuItem resetMenuItem = new MenuItem(Guid.NewGuid(), "Reset", "Reset", false, AddCommand, "Hidden");
            menuItems.Add(resetMenuItem);
            for (int i = 0; i < 4; i++)
            {
                MenuItem newMenu = new MenuItem(Guid.NewGuid(), "Monitor " + i , "Monitor " + i , false, AddCommand, "Hidden");
                
              if (i < 6)
                {
                    newMenu.SubMenuItems = new ObservableCollection<MenuItem>();
                    
                    MenuItem resMenuItem = new MenuItem(Guid.NewGuid(), "Resolution", "Resolution", false, AddCommand, "Hidden");
                    newMenu.SubMenuItems.Add(resMenuItem);
                    resMenuItem.SubMenuItems = new ObservableCollection<MenuItem>();

                    //change to use the finding possible resolutions function
                    for (int j = 0; j< 4; j++)
                    {
                        MenuItem resolutionChoiceMenuItem = new MenuItem(Guid.NewGuid(), "Resolution " + j  , "Resolution " + j , false, AddCommand, "Hidden");
                        resMenuItem.SubMenuItems.Add(resolutionChoiceMenuItem); 
                    }

                    MenuItem DPIMenuItem = new MenuItem(Guid.NewGuid(), "DPI", "DPI", false, AddCommand, "Hidden");
                    newMenu.SubMenuItems.Add(DPIMenuItem);
                    DPIMenuItem.SubMenuItems = new ObservableCollection<MenuItem>();

                    //change to adding possible DPIS
                    for (int j = 0; j < 4; j++)
                    {
                        MenuItem DPIChoiceMenuItem = new MenuItem(Guid.NewGuid(), "DPI " + j , "DPI " + j , false, AddCommand, "Hidden");
                        DPIMenuItem.SubMenuItems.Add(DPIChoiceMenuItem);
                    }

                    MenuItem brightnessMenuItem = new MenuItem(Guid.NewGuid(), "Brightness", "Brightness", false, AddCommand, "Hidden");
                    newMenu.SubMenuItems.Add(brightnessMenuItem);

                    brightnessMenuItem.SubMenuItems = new ObservableCollection<MenuItem>();
                    MenuItem brightnessSliderBox = new MenuItem(Guid.NewGuid(), "Brightness Slider Box", "                  ", false, AddCommand, 50);
                    brightnessMenuItem.SubMenuItems.Add(brightnessSliderBox);
                }
                menuItems.Add(newMenu);
            }
            RaisePropertyChanged("MenuItems");
        }

        #region Properties

        private ObservableCollection<MenuItem> menuItems;

        public ObservableCollection<MenuItem> MenuItems
        {
            get { return menuItems; }
            set 
            {
                menuItems = value;
                RaisePropertyChanged("MenuItems");
            }
        }

        private MenuItem menuSelected;

        public MenuItem MenuSelected
        {
            get { return menuSelected; }
            set 
            {
                menuSelected = value;
                RaisePropertyChanged("MenuSelected");
            }
        }

        #endregion Properties

        #region Commands

        private RelayCommand<MenuItem> addCommand;
        public RelayCommand<MenuItem> AddCommand
        {
            get => addCommand;
            set => addCommand = value;

        }

        private bool AddCommandCanExecute(MenuItem menuItem)
        {
            if (menuItem != null )
            {
                return true;
            }
            return false;
        }

        private void AddCommandExecute(MenuItem menuItem)
        {
            MenuSelected = menuItem;
        }

        private bool ChangeResolutionCommandCanExecute(MenuItem menuItem)
        {
            if (menuItem != null)
            {
                return true;
            }
            return false;
        }

        private void ChangeResolutionCommandExecute(MenuItem menuItem)
        {
            MenuSelected = menuItem;
        }

        private bool ChangeDPICommandCanExecute(MenuItem menuItem)
        {
            if (menuItem != null)
            {
                return true;
            }
            return false;
        }

        private void ChangeDPICommandExecute(MenuItem menuItem)
        {
            MenuSelected = menuItem;
        }

        private bool ChangeBrightnessCommandCanExecute(MenuItem menuItem, int currentSliderValue)
        {
            if (menuItem != null)
            {
                return true;
            }
            return false;
        }

        private void ChangeBrightnessCommandExecute(MenuItem menuItem, int currentSliderValue)
        {
            MenuSelected = menuItem;
            MenuSelected.SliderValue = currentSliderValue;
        }


        #endregion Commands

    }
}
