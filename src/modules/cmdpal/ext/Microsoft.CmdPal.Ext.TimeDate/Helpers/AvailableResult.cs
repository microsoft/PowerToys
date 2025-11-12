// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate.Helpers;

internal sealed class AvailableResult
{
    /// <summary>
    /// Gets or sets the time/date value
    /// </summary>
    internal string Value { get; set; }

    /// <summary>
    /// Gets or sets the text used for the subtitle and as search term
    /// </summary>
    internal string Label { get; set; }

    /// <summary>
    /// Gets or sets an alternative search tag that will be evaluated if label doesn't match. For example we like to show the era on searches for 'year' too.
    /// </summary>
    internal string AlternativeSearchTag { get; set; }

    /// <summary>
    /// Gets or sets a value indicating the type of result
    /// </summary>
    internal ResultIconType IconType { get; set; }

    /// <summary>
    /// Gets or sets a value to show additional error details
    /// </summary>
    internal string ErrorDetails { get; set; } = string.Empty;

    /// <summary>
    /// Returns the path to the icon
    /// </summary>
    /// <param name="theme">Theme</param>
    /// <returns>Path</returns>
    public IconInfo GetIconInfo()
    {
        return IconType switch
        {
            ResultIconType.Time => Icons.TimeIcon,
            ResultIconType.Date => Icons.CalendarIcon,
            ResultIconType.DateTime => Icons.TimeDateIcon,
            ResultIconType.Error => Icons.ErrorIcon,
            _ => null,
        };
    }

    public ListItem ToListItem()
    {
        return new ListItem(new CopyTextCommand(this.Value))
        {
            Title = this.Value,
            Subtitle = this.Label,
            Icon = this.GetIconInfo(),
            Details = string.IsNullOrEmpty(this.ErrorDetails) ? null : new Details() { Body = this.ErrorDetails },
        };
    }

    public int Score(string query, string label, string tags)
    {
        // Get match for label (or for tags if label score is <1)
        var score = FuzzyStringMatcher.ScoreFuzzy(query, label);
        if (score < 1)
        {
            foreach (var t in tags.Split(";"))
            {
                var tagScore = FuzzyStringMatcher.ScoreFuzzy(query, t.Trim()) / 2;
                if (tagScore > score)
                {
                    score = tagScore / 2;
                }
            }
        }

        return score;
    }
}

public enum ResultIconType
{
    Time,
    Date,
    DateTime,
    Error,
}
