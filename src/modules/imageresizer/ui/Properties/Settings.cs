// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using ImageResizer.Models;

namespace ImageResizer.Properties
{
    public partial class Settings : IDataErrorInfo, INotifyPropertyChanged
    {
        private string _fileNameFormat;

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
                {
                    index = Sizes.Count;
                }

                SelectedSizeIndex = index;
            }
        }

        string IDataErrorInfo.Error
            => string.Empty;

        public object this[string propertyName]
        {
            get
            {
                return base[propertyName];
            }

            set
            {
                base[propertyName] = value;
                if (propertyName == nameof(FileName))
                {
                    _fileNameFormat = null;
                }
            }
        }

        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                if (columnName != nameof(JpegQualityLevel))
                {
                    return string.Empty;
                }

                if (JpegQualityLevel < 1 || JpegQualityLevel > 100)
                {
                    return string.Format(Resources.ValueMustBeBetween, 1, 100);
                }

                return string.Empty;
            }
        }

        private class AllSizesCollection : IEnumerable<ResizeSize>, INotifyCollectionChanged, INotifyPropertyChanged
        {
            private ObservableCollection<ResizeSize> _sizes;
            private CustomSize _customSize;

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

            private void HandleCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
                => OnCollectionChanged(e);

            private void HandlePropertyChanged(object sender, PropertyChangedEventArgs e)
                => PropertyChanged?.Invoke(this, e);

            private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
                => CollectionChanged?.Invoke(this, e);

            IEnumerator IEnumerable.GetEnumerator()
                => GetEnumerator();

            private class AllSizesEnumerator : IEnumerator<ResizeSize>
            {
                private readonly AllSizesCollection _list;

                private int _index = -1;

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

        private static Settings defaultInstance = (Settings)System.Configuration.ApplicationSettingsBase.Synchronized(new Settings());

        public static Settings Default
        {
            get
            {
                return defaultInstance;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        [global::System.Configuration.DefaultSettingValueAttribute("0")]
        public int SelectedSizeIndex
        {
            get
            {
                return (int)this["SelectedSizeIndex"];
            }

            set
            {
                this["SelectedSizeIndex"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool ShrinkOnly
        {
            get
            {
                return (bool)this["ShrinkOnly"];
            }

            set
            {
                this["ShrinkOnly"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool Replace
        {
            get
            {
                return (bool)this["Replace"];
            }

            set
            {
                this["Replace"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool IgnoreOrientation
        {
            get
            {
                return (bool)this["IgnoreOrientation"];
            }

            set
            {
                this["IgnoreOrientation"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        [global::System.Configuration.DefaultSettingValueAttribute("90")]
        public int JpegQualityLevel
        {
            get
            {
                return (int)this["JpegQualityLevel"];
            }

            set
            {
                this["JpegQualityLevel"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        [global::System.Configuration.DefaultSettingValueAttribute("Default")]
        public global::System.Windows.Media.Imaging.PngInterlaceOption PngInterlaceOption
        {
            get
            {
                return (System.Windows.Media.Imaging.PngInterlaceOption)this["PngInterlaceOption"];
            }

            set
            {
                this["PngInterlaceOption"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        [global::System.Configuration.DefaultSettingValueAttribute("Default")]
        public global::System.Windows.Media.Imaging.TiffCompressOption TiffCompressOption
        {
            get
            {
                return (System.Windows.Media.Imaging.TiffCompressOption)this["TiffCompressOption"];
            }

            set
            {
                this["TiffCompressOption"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        [global::System.Configuration.DefaultSettingValueAttribute("%1 (%2)")]
        public string FileName
        {
            get
            {
                return (string)this["FileName"];
            }

            set
            {
                this["FileName"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        [global::System.Configuration.DefaultSettingValueAttribute(@"
          <ArrayOfResizeSize xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
            <ResizeSize>
              <Name>$small$</Name>
              <Fit>Fit</Fit>
              <Width>854</Width>
              <Height>480</Height>
              <Unit>Pixel</Unit>
            </ResizeSize>
            <ResizeSize>
              <Name>$medium$</Name>
              <Fit>Fit</Fit>
              <Width>1366</Width>
              <Height>768</Height>
              <Unit>Pixel</Unit>
            </ResizeSize>
            <ResizeSize>
              <Name>$large$</Name>
              <Fit>Fit</Fit>
              <Width>1920</Width>
              <Height>1080</Height>
              <Unit>Pixel</Unit>
            </ResizeSize>
            <ResizeSize>
              <Name>$phone$</Name>
              <Fit>Fit</Fit>
              <Width>320</Width>
              <Height>569</Height>
              <Unit>Pixel</Unit>
            </ResizeSize>
          </ArrayOfResizeSize>
        ")]
        public global::System.Collections.ObjectModel.ObservableCollection<ImageResizer.Models.ResizeSize> Sizes
        {
            get
            {
                return (ObservableCollection<ResizeSize>)this["Sizes"];
            }

            set
            {
                this["Sizes"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool KeepDateModified
        {
            get
            {
                return (bool)this["KeepDateModified"];
            }

            set
            {
                this["KeepDateModified"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        [global::System.Configuration.DefaultSettingValueAttribute("19e4a5aa-5662-4fc5-a0c0-1758028e1057")]
        public global::System.Guid FallbackEncoder
        {
            get
            {
                return (Guid)this["FallbackEncoder"];
            }

            set
            {
                this["FallbackEncoder"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        [global::System.Configuration.DefaultSettingValueAttribute(@"
          <CustomSize xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
            <Name>Custom</Name>
            <Fit>Fit</Fit>
            <Width>1024</Width>
            <Height>640</Height>
            <Unit>Pixel</Unit>
          </CustomSize>
        ")]
        public global::ImageResizer.Models.CustomSize CustomSize
        {
            get
            {
                return (CustomSize)this["CustomSize"];
            }

            set
            {
                this["CustomSize"] = value;
            }
        }

        [global::System.Configuration.UserScopedSettingAttribute]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool UpgradeRequired
        {
            get
            {
                return (bool)this["UpgradeRequired"];
            }

            set
            {
                this["UpgradeRequired"] = value;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
