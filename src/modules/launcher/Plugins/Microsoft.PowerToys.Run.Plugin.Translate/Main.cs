// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;
using GTranslate.Results;
using GTranslate.Translators;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.Translate.Properties;
using ScipBe.Common.Office.OneNote;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.Translate
{
    /// <summary>
    /// A power launcher plugin to search across time zones.
    /// </summary>
    public class Main : IPlugin, IPluginI18n, IDisposable
    {
        /// <summary>
        /// A value indicating if the Translate interop library was able to successfully initialize.
        /// </summary>
        private AggregateTranslator _translator;

        /// <summary>
        /// The initial context for this plugin (contains API and meta-data)
        /// </summary>
        private PluginInitContext? _context;

        /// <summary>
        /// The path to the icon for each result
        /// </summary>
        private string _iconPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> class.
        /// </summary>
        public Main()
        {
            UpdateIconPath(Theme.Light);

            _translator = new AggregateTranslator();
        }

        /// <summary>
        /// Gets the localized name.
        /// </summary>
        public string Name => Resources.PluginTitle;

        /// <summary>
        /// Gets the localized description.
        /// </summary>
        public string Description => Resources.PluginDescription;

        /// <summary>
        /// Initialize the plugin with the given <see cref="PluginInitContext"/>
        /// </summary>
        /// <param name="context">The <see cref="PluginInitContext"/> for this plugin</param>
        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        /// <summary>
        /// Return a filtered list, based on the given query
        /// </summary>
        /// <param name="query">The query to filter the list</param>
        /// <returns>A filtered list, can be empty when nothing was found</returns>
        public List<Result> Query(Query query)
        {
            if (query is null || string.IsNullOrWhiteSpace(query.Search))
            {
                return new List<Result>(0);
            }

            var translation = _translator.TranslateAsync(query.Search, "ru");
            var completed = translation.Wait(5000);

            ITranslationResult? translationResult = null;
            if (completed)
            {
                translationResult = translation.Result;
            }

            /*var pages = OneNoteProvider.FindPages(query.Search);

            return pages.Select(p => new Result
            {
                IcoPath = _iconPath,
                Title = p.Name,
                QueryTextDisplay = p.Name,
                SubTitle = @$"{p.Notebook.Name}\{p.Section.Name}",
                Action = (_) =>
                {
                    p.OpenInOneNote();
                    Console.WriteLine("show");
                    return true;
                },
                ContextData = p,
                ToolTipData = new ToolTipData(Name, @$"{p.Notebook.Name}\{p.Section.Name}\{p.Name}"),
            }).ToList();*/

            var translatedText = translationResult?.Translation ?? Resources.TranslateError;

            return new List<Result>()
            {
                new Result
                {
                    IcoPath = _iconPath,
                    Title = translatedText,
                    QueryTextDisplay = translatedText,
                    SubTitle = Resources.CopyTranslation,
                    Action = _ => TryToCopyToClipBoard(translatedText),
                    ToolTipData = new ToolTipData(Name, translationResult != null ? $"Source: {translationResult.Source};\nInput text: {translationResult.SourceLanguage.Name}" : string.Empty),
                },
            };
        }

        /// <summary>
        /// Copy the given text to the clipboard
        /// </summary>
        /// <param name="text">The text to copy to the clipboard</param>
        /// <returns><see langword="true"/>The text successful copy to the clipboard, otherwise <see langword="false"/></returns>
        private static bool TryToCopyToClipBoard(in string text)
        {
            try
            {
                Clipboard.Clear();
                Clipboard.SetText(text);
                return true;
            }
            catch (Exception exception)
            {
                Log.Exception("Can't copy to clipboard", exception, typeof(Main));
                return false;
            }
        }

        /// <summary>
        /// Return the translated plugin title.
        /// </summary>
        /// <returns>A translated plugin title.</returns>
        public string GetTranslatedPluginTitle() => Resources.PluginTitle;

        /// <summary>
        /// Return the translated plugin description.
        /// </summary>
        /// <returns>A translated plugin description.</returns>
        public string GetTranslatedPluginDescription() => Resources.PluginDescription;

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        [MemberNotNull(nameof(_iconPath))]
        private void UpdateIconPath(Theme theme)
        {
            // Icon author: Freepik
            _iconPath = theme == Theme.Light || theme == Theme.HighContrastWhite ? "Images/translate.light.png" : "Images/translate.dark.png";
        }

        public void Dispose()
        {
            // _translator?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
