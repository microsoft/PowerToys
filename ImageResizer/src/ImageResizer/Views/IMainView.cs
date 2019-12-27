using System.Collections.Generic;
using ImageResizer.ViewModels;

namespace ImageResizer.Views
{
    public interface IMainView
    {
        void Close();
        void ShowAdvanced(AdvancedViewModel viewModel);
        IEnumerable<string> OpenPictureFiles();
    }
}
