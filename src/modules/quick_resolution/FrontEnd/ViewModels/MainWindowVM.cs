using MenusWPF.Helpers.WpfApp1;
using MenusWPF.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Threading;

namespace MenusWPF.ViewModels
{

    public class MainWindowVM : INotifyPropertyChanged
    {

        [DllImport("SettingsLibrary.dll")]
        public static extern bool setResolution(String displayName, int pixelWidth, int pixelHeight);

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        [DllImport("SettingsLibrary.dll")]
        public static extern void getAllDisplaySettings(out MonitorResolutionSettings r);

        public MainWindowVM()
        {
            AddCommand = new RelayCommand<MenuItem>(AddCommandExecute, AddCommandCanExecute);

            RelayCommand<MenuItem>  resolutionCommands = new RelayCommand<MenuItem>(ChangeResolutionCommandExecute, AddCommandCanExecute);

            MonitorMenu = new ObservableCollection<MenuItem>();
            //MenuItem resetMenuItem = new MenuItem(Guid.NewGuid(), "Reset", false, AddCommand, "Hidden");
            //menuItems.Add(resetMenuItem);


            MonitorResolutionSettings resolutionSettings = new MonitorResolutionSettings();
            getAllDisplaySettings(out resolutionSettings);

                MenuItem monitorMenuItem = new MenuItem(Guid.NewGuid(), resolutionSettings.monitorName, false, AddCommand, "Hidden");
                
                monitorMenuItem.SubMenuItems = new ObservableCollection<MenuItem>();
                    
                MenuItem resMenuItem = new MenuItem(Guid.NewGuid(), "Resolution", false, resolutionCommands, "Hidden");
                monitorMenuItem.SubMenuItems.Add(resMenuItem);
                resMenuItem.SubMenuItems = new ObservableCollection<MenuItem>();

                // replace these once interops are working. 
                int w = resolutionSettings.res1.width;
                int h = resolutionSettings.res1.height;
                MenuItem resolutionChoiceMenuItem1 = new MenuItem(Guid.NewGuid(),  w +" x " + h, false, resolutionCommands, "Hidden");
                resMenuItem.SubMenuItems.Add(resolutionChoiceMenuItem1);

                w = resolutionSettings.res2.width;
                h = resolutionSettings.res2.height;
                MenuItem resolutionChoiceMenuItem2 = new MenuItem(Guid.NewGuid(),  w + " x " + h, false, resolutionCommands, "Hidden");
                resMenuItem.SubMenuItems.Add(resolutionChoiceMenuItem2);

                w = resolutionSettings.res3.width;
                h = resolutionSettings.res3.height;
                MenuItem resolutionChoiceMenuItem3 = new MenuItem(Guid.NewGuid(),  w + " x " + h, false, resolutionCommands, "Hidden");
                resMenuItem.SubMenuItems.Add(resolutionChoiceMenuItem3);

                w = resolutionSettings.res4.width;
                h = resolutionSettings.res4.height;
                MenuItem resolutionChoiceMenuItem4 = new MenuItem(Guid.NewGuid(),  w + " x " + h, false, resolutionCommands, "Hidden");
                resMenuItem.SubMenuItems.Add(resolutionChoiceMenuItem4);


                w = resolutionSettings.res5.width;
                h = resolutionSettings.res5.height;
                MenuItem resolutionChoiceMenuItem5 = new MenuItem(Guid.NewGuid(), w + " x " + h, false, resolutionCommands, "Hidden");
                resMenuItem.SubMenuItems.Add(resolutionChoiceMenuItem5);


            //for (int j = 0; j< 4; j++)
            //{
            //    MenuItem resolutionChoiceMenuItem = new MenuItem(Guid.NewGuid(), "Resolution " + j  , "Resolution " + j , false, AddCommand, "Hidden");
            //    resMenuItem.SubMenuItems.Add(resolutionChoiceMenuItem); 
            //}

            //MenuItem DPIMenuItem = new MenuItem(Guid.NewGuid(), "DPI", "DPI", false, AddCommand, "Hidden");
            //newMenu.SubMenuItems.Add(DPIMenuItem);
            //DPIMenuItem.SubMenuItems = new ObservableCollection<MenuItem>();

            ////change to adding possible DPIS
            //for (int j = 0; j < 4; j++)
            //{
            //    MenuItem DPIChoiceMenuItem = new MenuItem(Guid.NewGuid(), "DPI " + j , "DPI " + j , false, AddCommand, "Hidden");
            //    DPIMenuItem.SubMenuItems.Add(DPIChoiceMenuItem);
            //}

            MenuItem brightnessMenuItem = new MenuItem(Guid.NewGuid(), "Brightness", false, AddCommand, "Hidden");
            monitorMenuItem.SubMenuItems.Add(brightnessMenuItem);

            brightnessMenuItem.SubMenuItems = new ObservableCollection<MenuItem>();
            MenuItem brightnessSliderBox = new MenuItem(Guid.NewGuid(), "Brightness Slider Box", "                  ", false, AddCommand, 50);
            brightnessMenuItem.SubMenuItems.Add(brightnessSliderBox);

            MonitorMenu.Add(monitorMenuItem);

            RaisePropertyChanged("MenuItems");
        }

        #region Properties

        private ObservableCollection<MenuItem> MonitorMenu;

        public ObservableCollection<MenuItem> MenuItems
        {
            get { return MonitorMenu; }
            set 
            {
                MonitorMenu = value;
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
            bool success = setResolution("\\\\.\\DISPLAY1", 1920, 1080);
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
