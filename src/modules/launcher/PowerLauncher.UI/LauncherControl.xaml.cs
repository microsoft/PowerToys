using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace PowerToysUX.Controls
{
    public sealed partial class PowerLaunch : UserControl
    {
        List<SearchApp> SearchResults;
        public PowerLaunch()
        {
            this.InitializeComponent();
            ShellBarShadow.Receivers.Add(ShadowReceiverGrid);
            SearchResults = new List<SearchApp>
            {
                new SearchApp() { Title = "Calendar", Icon = "ms-appx:///Assets/Images/Calendar.png" },
                new SearchApp() { Title = "Camera", Icon = "ms-appx:///Assets/Images/Camera.png" },
                new SearchApp() { Title = "Excel", Icon = "ms-appx:///Assets/Images/Excel.png" },
                new SearchApp() { Title = "Feedback", Icon = "ms-appx:///Assets/Images/Feedback.png" },
                new SearchApp() { Title = "File Explorer", Icon = "ms-appx:///Assets/Images/FileExplorer.png" },
                new SearchApp() { Title = "Groove", Icon = "ms-appx:///Assets/Images/Groove.png" },
                new SearchApp() { Title = "Mail", Icon = "ms-appx:///Assets/Images/Mail.png" },
                new SearchApp() { Title = "Mobile", Icon = "ms-appx:///Assets/Images/Mobile.png" },
                new SearchApp() { Title = "Movies", Icon = "ms-appx:///Assets/Images/Movies.png" },
                new SearchApp() { Title = "OneDrive", Icon = "ms-appx:///Assets/Images/OneDrive.png" },
                new SearchApp() { Title = "OneNote", Icon = "ms-appx:///Assets/Images/OneNote.png" },
                new SearchApp() { Title = "Outlook", Icon = "ms-appx:///Assets/Images/Outlook.png" },
                new SearchApp() { Title = "Photos", Icon = "ms-appx:///Assets/Images/Photos.png" },
                new SearchApp() { Title = "PowerPoint", Icon = "ms-appx:///Assets/Images/PowerPoint.png" },
                new SearchApp() { Title = "PowerToys", Icon = "ms-appx:///Assets/Images/PowerToysIcon.png" },
                new SearchApp() { Title = "Screen Sketch", Icon = "ms-appx:///Assets/Images/ScreenSketch.png" },
                new SearchApp() { Title = "SharePoint", Icon = "ms-appx:///Assets/Images/SharePoint.png" },
                new SearchApp() { Title = "Skype", Icon = "ms-appx:///Assets/Images/Skype.png" },
                new SearchApp() { Title = "Solitaire", Icon = "ms-appx:///Assets/Images/Solitaire.png" },
                new SearchApp() { Title = "Teams", Icon = "ms-appx:///Assets/Images/Teams.png" },
                new SearchApp() { Title = "Weather", Icon = "ms-appx:///Assets/Images/Weather.png" },
                new SearchApp() { Title = "Whiteboard", Icon = "ms-appx:///Assets/Images/Whiteboard.png" },
                new SearchApp() { Title = "Word", Icon = "ms-appx:///Assets/Images/Word.png" },
                new SearchApp() { Title = "Yammer", Icon = "ms-appx:///Assets/Images/Yammer.png" }
            };
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var Suggestion = SearchResults.Where(p => p.Title.StartsWith(sender.Text, StringComparison.OrdinalIgnoreCase)).ToArray();
                sender.ItemsSource = Suggestion;
            }
        }
    }


    public class SearchApp
    {
        public string Title { get; set; }
        public string Icon { get; set; }
        public string Path { get; set; }
    }
}
