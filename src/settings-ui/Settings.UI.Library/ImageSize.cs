// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Settings.UI.Library.Resources;

namespace Microsoft.PowerToys.Settings.UI.Library;

public partial class ImageSize : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        bool changed = !EqualityComparer<T>.Default.Equals(field, value);
        if (changed)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccessibleTextHelper)));
        }

        return changed;
    }

    public ImageSize(int id = 0, string name = "", ResizeFit fit = ResizeFit.Fit, double width = 0, double height = 0, ResizeUnit unit = ResizeUnit.Pixel)
    {
        Id = id;
        Name = name;
        Fit = fit;
        Width = width;
        Height = height;
        Unit = unit;
    }

    private int _id;
    private string _name;
    private ResizeFit _fit;
    private double _height;
    private double _width;
    private ResizeUnit _unit;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>
    /// Gets a value indicating whether the <see cref="Height"/> property is used. When false, the
    /// <see cref="Width"/> property is used to evenly scale the image in both X and Y dimensions.
    /// </summary>
    [JsonIgnore]
    public bool IsHeightUsed
    {
        // Height is ignored when using percentage scaling where the aspect ratio is maintained
        // (i.e. non-stretch fits). In all other cases, both Width and Height are needed.
        get => !(Unit == ResizeUnit.Percent && Fit != ResizeFit.Stretch);
    }

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    [JsonPropertyName("fit")]
    public ResizeFit Fit
    {
        get => _fit;
        set
        {
            if (SetProperty(ref _fit, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHeightUsed)));
            }
        }
    }

    [JsonPropertyName("width")]
    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value < 0 || double.IsNaN(value) ? 0 : value);
    }

    [JsonPropertyName("height")]
    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, value < 0 || double.IsNaN(value) ? 0 : value);
    }

    [JsonPropertyName("unit")]
    public ResizeUnit Unit
    {
        get => _unit;
        set
        {
            if (SetProperty(ref _unit, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsHeightUsed)));
            }
        }
    }

    /// <summary>
    /// Gets access to all properties for formatting accessibility descriptions.
    /// </summary>
    [JsonIgnore]
    public ImageSize AccessibleTextHelper => this;

    public string ToJsonString() => JsonSerializer.Serialize(this);
}
