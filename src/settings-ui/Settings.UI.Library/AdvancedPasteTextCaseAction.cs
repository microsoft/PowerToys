// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library;

public sealed class AdvancedPasteTextCaseAction : Observable, IAdvancedPasteAction
{
    public static class PropertyNames
    {
        public const string LowerCase = "lower-case";
        public const string UpperCase = "upper-case";
        public const string TitleCase = "title-case";
        public const string SentenceCase = "sentence-case";
        public const string ToggleCase = "toggle-case";
        public const string CamelCase = "camel-case";
        public const string PascalCase = "pascal-case";
        public const string SnakeCase = "snake-case";
        public const string ScreamingSnakeCase = "screaming-snake-case";
        public const string KebabCase = "kebab-case";
    }

    private AdvancedPasteAdditionalAction _lowerCase = new();
    private AdvancedPasteAdditionalAction _upperCase = new();
    private AdvancedPasteAdditionalAction _titleCase = new();
    private AdvancedPasteAdditionalAction _sentenceCase = new();
    private AdvancedPasteAdditionalAction _toggleCase = new();
    private AdvancedPasteAdditionalAction _camelCase = new();
    private AdvancedPasteAdditionalAction _pascalCase = new();
    private AdvancedPasteAdditionalAction _snakeCase = new();
    private AdvancedPasteAdditionalAction _screamingSnakeCase = new();
    private AdvancedPasteAdditionalAction _kebabCase = new();
    private bool _isShown = true;

    [JsonPropertyName("isShown")]
    public bool IsShown
    {
        get => _isShown;
        set => Set(ref _isShown, value);
    }

    [JsonPropertyName(PropertyNames.LowerCase)]
    public AdvancedPasteAdditionalAction LowerCase
    {
        get => _lowerCase;
        init => Set(ref _lowerCase, value ?? new());
    }

    [JsonPropertyName(PropertyNames.UpperCase)]
    public AdvancedPasteAdditionalAction UpperCase
    {
        get => _upperCase;
        init => Set(ref _upperCase, value ?? new());
    }

    [JsonPropertyName(PropertyNames.TitleCase)]
    public AdvancedPasteAdditionalAction TitleCase
    {
        get => _titleCase;
        init => Set(ref _titleCase, value ?? new());
    }

    [JsonPropertyName(PropertyNames.SentenceCase)]
    public AdvancedPasteAdditionalAction SentenceCase
    {
        get => _sentenceCase;
        init => Set(ref _sentenceCase, value ?? new());
    }

    [JsonPropertyName(PropertyNames.ToggleCase)]
    public AdvancedPasteAdditionalAction ToggleCase
    {
        get => _toggleCase;
        init => Set(ref _toggleCase, value ?? new());
    }

    [JsonPropertyName(PropertyNames.CamelCase)]
    public AdvancedPasteAdditionalAction CamelCase
    {
        get => _camelCase;
        init => Set(ref _camelCase, value ?? new());
    }

    [JsonPropertyName(PropertyNames.PascalCase)]
    public AdvancedPasteAdditionalAction PascalCase
    {
        get => _pascalCase;
        init => Set(ref _pascalCase, value ?? new());
    }

    [JsonPropertyName(PropertyNames.SnakeCase)]
    public AdvancedPasteAdditionalAction SnakeCase
    {
        get => _snakeCase;
        init => Set(ref _snakeCase, value ?? new());
    }

    [JsonPropertyName(PropertyNames.ScreamingSnakeCase)]
    public AdvancedPasteAdditionalAction ScreamingSnakeCase
    {
        get => _screamingSnakeCase;
        init => Set(ref _screamingSnakeCase, value ?? new());
    }

    [JsonPropertyName(PropertyNames.KebabCase)]
    public AdvancedPasteAdditionalAction KebabCase
    {
        get => _kebabCase;
        init => Set(ref _kebabCase, value ?? new());
    }

    [JsonIgnore]
    public IEnumerable<IAdvancedPasteAction> SubActions =>
    [
        LowerCase,
        UpperCase,
        TitleCase,
        SentenceCase,
        ToggleCase,
        CamelCase,
        PascalCase,
        SnakeCase,
        ScreamingSnakeCase,
        KebabCase,
    ];
}
