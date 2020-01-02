using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using ImageResizer.Models;

namespace ImageResizer.Properties
{
    partial class Settings : IDataErrorInfo
    {
        string _fileNameFormat;

        public Settings()
            => AllSizes = new AllSizesCollection(this);

        public IEnumerable<ResizeSize> AllSizes { get; }

        public string FileNameFormat
            => _fileNameFormat
                ?? (_fileNameFormat = FileName
                    .Replace("{", "{{")
                    .Replace("}", "}}")
                    .Replace("%1", "{0}")
                    .Replace("%2", "{1}")
                    .Replace("%3", "{2}")
                    .Replace("%4", "{3}")
                    .Replace("%5", "{4}")
                    .Replace("%6", "{5}"));

        public ResizeSize SelectedSize
        {
            get => SelectedSizeIndex >= 0 && SelectedSizeIndex < Sizes.Count
                    ? Sizes[SelectedSizeIndex]
                    : CustomSize;
            set
            {
                var index = Sizes.IndexOf(value);
                if (index == -1)
                    index = Sizes.Count;

                SelectedSizeIndex = index;
            }
        }

        string IDataErrorInfo.Error
            => string.Empty;

        public override object this[string propertyName]
        {
            get { return base[propertyName]; }
            set
            {
                base[propertyName] = value;

                if (propertyName == nameof(FileName))
                    _fileNameFormat = null;
            }
        }

        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                if (columnName != nameof(JpegQualityLevel))
                    return string.Empty;

                if (JpegQualityLevel < 1 || JpegQualityLevel > 100)
                    return string.Format(Resources.ValueMustBeBetween, 1, 100);

                return string.Empty;
            }
        }

        class AllSizesCollection : IEnumerable<ResizeSize>, INotifyCollectionChanged, INotifyPropertyChanged
        {
            ObservableCollection<ResizeSize> _sizes;
            CustomSize _customSize;

            public AllSizesCollection(Settings settings)
            {
                _sizes = settings.Sizes;
                _customSize = settings.CustomSize;

                _sizes.CollectionChanged += HandleCollectionChanged;
                ((INotifyPropertyChanged)_sizes).PropertyChanged += HandlePropertyChanged;

                settings.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(CustomSize))
                    {
                        var oldCustomSize = _customSize;
                        _customSize = settings.CustomSize;

                        OnCollectionChanged(
                            new NotifyCollectionChangedEventArgs(
                                NotifyCollectionChangedAction.Replace,
                                _customSize,
                                oldCustomSize,
                                _sizes.Count));
                    }
                    else if (e.PropertyName == nameof(Sizes))
                    {
                        var oldSizes = _sizes;

                        oldSizes.CollectionChanged -= HandleCollectionChanged;
                        ((INotifyPropertyChanged)oldSizes).PropertyChanged -= HandlePropertyChanged;

                        _sizes = settings.Sizes;

                        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

                        _sizes.CollectionChanged += HandleCollectionChanged;
                        ((INotifyPropertyChanged)_sizes).PropertyChanged += HandlePropertyChanged;
                    }
                };
            }

            public event NotifyCollectionChangedEventHandler CollectionChanged;
            public event PropertyChangedEventHandler PropertyChanged;

            public int Count
                => _sizes.Count + 1;

            public ResizeSize this[int index]
                => index == _sizes.Count
                    ? _customSize
                    : _sizes[index];

            public IEnumerator<ResizeSize> GetEnumerator()
                => new AllSizesEnumerator(this);

            void HandleCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
                => OnCollectionChanged(e);

            void HandlePropertyChanged(object sender, PropertyChangedEventArgs e)
                => PropertyChanged?.Invoke(this, e);

            void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
                => CollectionChanged?.Invoke(this, e);

            IEnumerator IEnumerable.GetEnumerator()
                => GetEnumerator();

            class AllSizesEnumerator : IEnumerator<ResizeSize>
            {
                readonly AllSizesCollection _list;

                int _index = -1;

                public AllSizesEnumerator(AllSizesCollection list)
                    => _list = list;

                public ResizeSize Current
                    => _list[_index];

                object IEnumerator.Current
                    => Current;

                public void Dispose()
                {
                }

                public bool MoveNext()
                    => ++_index < _list.Count;

                public void Reset()
                    => _index = -1;
            }
        }
    }
}
