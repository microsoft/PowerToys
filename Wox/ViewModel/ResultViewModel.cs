using System;
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
            }
        }


        #endregion

        #region ViewModel Properties

        public string Title => RawResult.Title;

        public string SubTitle => RawResult.SubTitle;

        public string FullIcoPath => RawResult.FullIcoPath;

        public string PluginID => RawResult.PluginID;
        public int Score
        {
            get { return RawResult.Score; }
            set { RawResult.Score = value; }
        }

        public Query OriginQuery
        {
            get { return RawResult.OriginQuery; }
            set { RawResult.OriginQuery = value; }
        }

        public Func<ActionContext, bool> Action
        {
            get { return RawResult.Action; }
            set { RawResult.Action = value; }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Properties

        internal Result RawResult { get; }

        #endregion

        public void Update(ResultViewModel newResult)
        {
            RawResult.Score = newResult.RawResult.Score;
            RawResult.OriginQuery = newResult.RawResult.OriginQuery;
        }

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
