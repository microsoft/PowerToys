using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FancyZonesEditor
{
    public class MulticlassDataContext : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public MonitorVM MonitorVM { get; set; }

        private readonly Settings _settings = ((App)Application.Current).ZoneSettings;

    }
}
