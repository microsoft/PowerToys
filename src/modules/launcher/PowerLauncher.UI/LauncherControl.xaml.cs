using Windows.UI.Xaml.Controls;

namespace PowerLauncher.UI
{
    public sealed partial class LauncherControl : UserControl
    {
        public LauncherControl()
        {
            InitializeComponent();
        }

        private void OnKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var NextResultCommand = this.DataContext.GetType().GetMethod("get_SelectNextItemCommand").Invoke(this.DataContext, new object[] { });
            var NextResult = NextResultCommand.GetType().GetMethod("Execute");

            var PreviousResultCommand = this.DataContext.GetType().GetMethod("get_SelectPrevItemCommand").Invoke(this.DataContext, new object[] { });
            var PreviousResult = PreviousResultCommand.GetType().GetMethod("Execute");

            var NextPageResultCommand = this.DataContext.GetType().GetMethod("get_SelectNextPageCommand").Invoke(this.DataContext, new object[] { });
            var NextPageResult = NextResultCommand.GetType().GetMethod("Execute");

            var PreviousPageResultCommand = this.DataContext.GetType().GetMethod("get_SelectPrevPageCommand").Invoke(this.DataContext, new object[] { });
            var PreviousPageResult = PreviousResultCommand.GetType().GetMethod("Execute");

            if (e.Key == Windows.System.VirtualKey.Down)
            {
                NextResult.Invoke(NextResultCommand, new object[] { null });
            }
            else if (e.Key == Windows.System.VirtualKey.Up)
            {
                PreviousResult.Invoke(PreviousResultCommand, new object[] { null });
            }
            else if (e.Key == Windows.System.VirtualKey.PageDown)
            {
                NextPageResult.Invoke(NextPageResultCommand, new object[] { null });
            }
            else if (e.Key == Windows.System.VirtualKey.PageUp)
            {
                PreviousPageResult.Invoke(PreviousPageResultCommand, new object[] { null });
            }
        }
    }
}