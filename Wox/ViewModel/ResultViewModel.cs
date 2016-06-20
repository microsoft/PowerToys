using System;
using System.Windows.Media;
using System.Windows;
using Wox.Infrastructure.Image;
using Wox.Plugin;


namespace Wox.ViewModel
{
    public class ResultViewModel : BaseModel
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

        public string PluginID => RawResult.PluginID;

        public ImageSource Image => ImageLoader.Load(RawResult.IcoPath);

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
