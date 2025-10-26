#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

using ImageResizer.Helpers;
using ImageResizer.Models.ResizeResults;
using ImageResizer.Properties;
using ImageResizer.Views;

namespace ImageResizer.ViewModels;

public class ResultsViewModel : Observable
{
    private readonly IMainView _mainView;

    /// <summary>
    /// The full list of results from the resizing operation(s).
    /// </summary>
    private readonly List<ResizeResult> _allResults;

    /// <summary>
    /// Gets or sets the text displayed at the top of the page, indicating whether the page is
    /// being shown because of errors or warnings/info.
    /// </summary>
    public string HeaderText { get; set; }

    public IEnumerable<ResultDisplayModel> Errors =>
        _allResults.OfType<ErrorResult>()
            .Select(r => new ResultDisplayModel(r));

    public IEnumerable<ResultDisplayModel> ReplaceWarnings =>
        _allResults.OfType<FileReplaceFailedResult>()
            .Select(r => new ResultDisplayModel(r));

    public IEnumerable<ResultDisplayModel> RecycleFailedWarnings =>
        _allResults.OfType<FileRecycleFailedResult>()
            .Select(r => new ResultDisplayModel(r));

    public ResultsViewModel(IMainView mainView, IEnumerable<ResizeResult> results)
    {
        _mainView = mainView;
        CloseCommand = new RelayCommand(Close);

        _allResults = results.OrderBy(x => x.FilePath).ToList();

        HeaderText = Errors.Any()
            ? Resources.Results_PageHeader_CompleteWithErrors
            : Resources.Results_PageHeader_CompleteWithNotes;
    }

    public ICommand CloseCommand { get; }

    public void Close() => _mainView.Close();
}
