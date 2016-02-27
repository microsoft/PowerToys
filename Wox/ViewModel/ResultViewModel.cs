using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Infrastructure;
using Wox.Infrastructure.Hotkey;
using Wox.Plugin;
using Wox.Storage;

namespace Wox.ViewModel
{
    public class ResultViewModel : BaseViewModel
    {
        #region Private Fields

        private bool _isSelected;

        #endregion

        #region Constructor

        public ResultViewModel(Result result)
        {
            if (result != null)
            {
                RawResult = result;

                OpenResultListBoxItemCommand = new RelayCommand(_ =>
                {

                    bool hideWindow = result.Action(new ActionContext
                    {
                        SpecialKeyState = GlobalHotkey.Instance.CheckModifiers()
                    });

                    if (hideWindow)
                    {
                        App.API.HideApp();
                        UserSelectedRecordStorage.Instance.Add(RawResult);
                        QueryHistoryStorage.Instance.Add(RawResult.OriginQuery.RawQuery);
                    }
                });

                OpenContextMenuItemCommand = new RelayCommand(_ =>
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

        public string Title => RawResult.Title;

        public string SubTitle => RawResult.SubTitle;

        public string FullIcoPath => RawResult.FullIcoPath;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand OpenResultListBoxItemCommand { get; set; }

        public RelayCommand OpenContextMenuItemCommand { get; set; }

        #endregion

        #region Properties

        public Result RawResult { get; }

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
                        App.API.ShowMsg("Succeed");
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
                        App.API.ShowMsg("Succeed");
                        return false;
                    }
                };
            }
        }


        #endregion

        public override bool Equals(object obj)
        {
            ResultViewModel r = obj as ResultViewModel;
            if (r != null)
            {
                return RawResult.Equals(r.RawResult);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return RawResult.GetHashCode();
        }

        public override string ToString()
        {
            return RawResult.ToString();
        }

    }
}
