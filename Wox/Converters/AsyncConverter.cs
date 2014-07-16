using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Data;
using System.Windows.Threading;

namespace Wox.Converters
{
    public class AsyncTask : INotifyPropertyChanged
    {
        public AsyncTask(Func<object> valueFunc)
        {
            LoadValue(valueFunc);
        }

        private void LoadValue(Func<object> valueFunc)
        {
            var frame = new DispatcherFrame();
            ThreadPool.QueueUserWorkItem(delegate
            {
            
                        object returnValue =
                        AsyncValue = valueFunc();
                        if (PropertyChanged != null)
                            PropertyChanged(this, new PropertyChangedEventArgs("AsyncValue"));
                
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public object AsyncValue
        {
            get;
            set;
        }
    }
}
