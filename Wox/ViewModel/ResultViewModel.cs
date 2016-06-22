using System.Windows.Media;
using Wox.Infrastructure.Image;
using Wox.Plugin;


namespace Wox.ViewModel
{
    public class ResultViewModel : BaseModel
    {
        public ResultViewModel(Result result)
        {
            if (result != null)
            {
                Result = result;
            }
        }

        public ImageSource Image => ImageLoader.Load(Result.IcoPath);

        public Result Result { get; }

        public override bool Equals(object obj)
        {
            var r = obj as ResultViewModel;
            if (r != null)
            {
                return Result.Equals(r.Result);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Result.GetHashCode();
        }

        public override string ToString()
        {
            return Result.ToString();
        }

    }
}
