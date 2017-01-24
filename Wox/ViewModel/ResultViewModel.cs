using System;
using System.Windows.Media;
using System.Windows.Threading;
using Wox.Infrastructure.Image;
using Wox.Infrastructure.Logger;
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

        public ImageSource Image
        {
            get
            {
                if (string.IsNullOrEmpty(Result.IcoPath))
                {
                    try
                    {
                        return Result.Icon();
                    }
                    catch (Exception e)
                    {
                        Log.Exception($"|ResultViewModel.Image|IcoPath is empty and exception when calling Icon() for result <{Result.Title}> of plugin <{Result.PluginDirectory}>", e);
                        return ImageLoader.Load(Result.IcoPath);
                    }
                }
                else
                {
                    return ImageLoader.Load(Result.IcoPath);
                }
            }
        }

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
