using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Infrastructure;
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
            if (null != result)
            {
                this._result = result;

                this.OpenResultListBoxItemCommand = new RelayCommand((parameter) =>
                {

                    bool hideWindow = result.Action(new ActionContext
                    {
                        SpecialKeyState = GlobalHotkey.Instance.CheckModifiers()
                    });

                    if (hideWindow)
                    {
                        App.API.HideApp();
                        UserSelectedRecordStorage.Instance.Add(this._result);
                        QueryHistoryStorage.Instance.Add(this._result.OriginQuery.RawQuery);
                    }
                });

                this.OpenContextMenuItemCommand = new RelayCommand((parameter) =>
                {

                    var actions = PluginManager.GetContextMenusForPlugin(result);

                    var pluginMetaData = PluginManager.GetPluginForId(result.PluginID).Metadata;
                    actions.ForEach(o =>
                    {
                        o.PluginDirectory = pluginMetaData.PluginDirectory;
                        o.PluginID = result.PluginID;
                        o.OriginQuery = result.OriginQuery;
                    });

                    actions.Add(GetTopMostContextMenu(result));

                    App.API.ShowContextMenu(pluginMetaData, actions);

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

        public RelayCommand OpenResultListBoxItemCommand { get; set; }

        public RelayCommand OpenContextMenuItemCommand { get; set; }

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

        #region Private Methods

        private Result GetTopMostContextMenu(Result result)
        {
            if (TopMostRecordStorage.Instance.IsTopMost(result))
            {
                return new Result(InternationalizationManager.Instance.GetTranslation("cancelTopMostInThisQuery"), "Images\\down.png")
                {
                    PluginDirectory = WoxDirectroy.Executable,
                    Action = _ =>
                    {
                        TopMostRecordStorage.Instance.Remove(result);
                        App.API.ShowMsg("Succeed", "", "");
                        return false;
                    }
                };
            }
            else
            {
                return new Result(InternationalizationManager.Instance.GetTranslation("setAsTopMostInThisQuery"), "Images\\up.png")
                {
                    PluginDirectory = WoxDirectroy.Executable,
                    Action = _ =>
                    {
                        TopMostRecordStorage.Instance.AddOrUpdate(result);
                        App.API.ShowMsg("Succeed", "", "");
                        return false;
                    }
                };
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

    }
}
