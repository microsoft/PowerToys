using Windows.UI.Xaml.Controls;

namespace PowerLauncher.ResultList
{
    public sealed partial class ResultList : UserControl
    {
        public ListView ResultsList => SuggestionsList;

        public ResultList()
        {
            InitializeComponent();
        }

        private void TextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            // SuggestionsPopup.IsOpen = true;
        }

        private void SuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] != null)
            {
                //ScrollIntoViewAlignment(e.AddedItems[0]);
            }
        }
    }
}