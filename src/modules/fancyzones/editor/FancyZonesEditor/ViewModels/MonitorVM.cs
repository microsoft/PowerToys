using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

namespace FancyZonesEditor
{

    public class MonitorChangedEventArgs : EventArgs
    {
        public readonly int LastMonitor;

        public MonitorChangedEventArgs(int lastMonitor)
        {
            LastMonitor = lastMonitor;
        }
    }

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
            set 
            {
                int _lastMonitor = _currentMonitor;
                _currentMonitor = value;
                CurrentMonitorChanged?.Invoke(new MonitorChangedEventArgs(_lastMonitor));
            }
        }

        public delegate void MonitorChangedEventHandler(MonitorChangedEventArgs args);
        public static event MonitorChangedEventHandler CurrentMonitorChanged;

        private static int _lastMonitor;

        public static int LastMonitor
        {
            get { return _lastMonitor; }
            set { _lastMonitor = value; }
        }

        public MonitorVM()
        {
            AddCommand = new RelayCommand(AddCommandExecute, AddCommandCanExecute);
            DeleteCommand = new RelayCommand(DeleteCommandExecute, DeleteCommandCanExecute);
            SelectCommand = new RelayCommand<MonitorInfo>(SelectCommandExecute, SelectCommandCanExecute);
            Monitors = new ObservableCollection<MonitorInfo>();
            for (int i = 0; i < App.NumMonitors; ++i)
            {
                Monitors.Add(new MonitorInfo(i, "Monitor " + i, 100, 150, MonitorVM.CurrentMonitor == i));
            }
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

        private RelayCommand<MonitorInfo> selectCommand;

        public RelayCommand<MonitorInfo> SelectCommand
        {
            get => selectCommand;
            set => selectCommand = value;
        }

        private bool SelectCommandCanExecute(MonitorInfo monitorInfo)
        {
            return true;
        }

        private void SelectCommandExecute(MonitorInfo monitorInfo)
        {
            MonitorVM.CurrentMonitor = monitorInfo.Id;
            App.Update();
            for (int i = 0; i < Monitors.Count; ++i)
            {
                if (Monitors[i].Selected)
                {
                    Monitors[i] = new MonitorInfo(Monitors[i].Id, Monitors[i].Name, Monitors[i].Height, Monitors[i].Width, false);
                    break;
                }
            }

            Monitors[monitorInfo.Id] = new MonitorInfo(monitorInfo.Id, monitorInfo.Name, monitorInfo.Height, monitorInfo.Width, true);
        }

        #endregion Commands

    }
}