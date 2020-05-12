using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Navigation;

namespace FancyZonesEditor
{
    public class MonitorVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private static int _currentMonitor;

        public static int CurrentMonitor
        {
            get { return _currentMonitor; }
            set { _currentMonitor = value; }
        }



        public MonitorVM()
        {
            AddCommand = new RelayCommand(AddCommandExecute, AddCommandCanExecute);
            DeleteCommand = new RelayCommand(DeleteCommandExecute, DeleteCommandCanExecute);

            Monitors = new ObservableCollection<MonitorInfo>();
            Monitors.Add(new MonitorInfo(0, "Monitor 1", 100, 150, "DeepSkyBlue"));
            Monitors.Add(new MonitorInfo(1, "Monitor 2", 100, 150));
            Monitors.Add(new MonitorInfo(2, "Monitor 3", 100, 150));
        }

        public void SwitchMonitor()
        {
            _currentMonitor = 1;
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
            get
            {
                return height;
            }

            set
            {
                height = value;
                RaisePropertyChanged(nameof(Height));
                AddCommand.RaiseCanExecuteChanged();
            }
        }

        private int width = 100;

        public int Width
        {
            get
            {
                return width;
            }

            set
            {
                width = value;
                RaisePropertyChanged(nameof(Width));
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
            return (bool)(Height > 0 && Width > 0);
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
}