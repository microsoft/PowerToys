using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wox.Infrastructure.Hotkey;
using Wox.Plugin;
using Wox.Storage;

namespace Wox.ViewModel
{
    public class ResultItemViewModel : BaseViewModel
    {
        #region Private Fields

        private Result _result;
        private bool _isSelected;

        #endregion

        #region Constructor

        public ResultItemViewModel(Result result)
        {
            if(null!= result)
            {
                this._result = result;

                this.OpenResultCommand = new RelayCommand((parameter) => {

                    bool hideWindow = result.Action(new ActionContext
                    {
                        SpecialKeyState = GlobalHotkey.Instance.CheckModifiers()
                    });

                    if (null != this.ResultOpened)
                    {
                        this.ResultOpened(this, new ResultOpenedEventArgs(hideWindow));
                    }
                });

                this.OpenResultActionPanelCommand = new RelayCommand((parameter) => {

                    if(null!= ResultActionPanelOpened)
                    {
                        this.ResultActionPanelOpened(this, new EventArgs());
                    }

                });
            }
        }

        #endregion

        #region ViewModel Properties

        public string Title
        {
            get
            {
                return this._result.Title;
            }
        }

        public string SubTitle
        {
            get
            {
                return this._result.SubTitle;
            }
        }

        public string FullIcoPath
        {
            get
            {
                return this._result.FullIcoPath;
            }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        public RelayCommand OpenResultCommand
        {
            get;
            set;
        }

        public RelayCommand OpenResultActionPanelCommand
        {
            get;
            set;
        }

        #endregion

        #region Properties

        public Result RawResult
        {
            get
            {
                return this._result;
            }
        }

        #endregion

        public override bool Equals(object obj)
        {
            ResultItemViewModel r = obj as ResultItemViewModel;
            if (r != null)
            {
                return _result.Equals(r.RawResult);
            }
            
            return false;
        }

        public override int GetHashCode()
        {
            return _result.GetHashCode();
        }

        public override string ToString()
        {
            return _result.ToString();
        }

        public event EventHandler<ResultOpenedEventArgs> ResultOpened;

        public event EventHandler ResultActionPanelOpened;

    }

    public class ResultOpenedEventArgs : EventArgs
    {

        public bool HideWindow
        {
            get;
            private set;
        }

        public ResultOpenedEventArgs(bool hideWindow)
        {
            this.HideWindow = hideWindow;
        }
    }
}
