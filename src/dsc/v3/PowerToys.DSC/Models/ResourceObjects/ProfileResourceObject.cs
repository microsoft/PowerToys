// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PowerToys.DSC.Models.KeyboardManager;

namespace PowerToys.DSC.Models.ResourceObjects;

/// <summary>
/// Represents the resource object for the Keyboard Manager remapping profile.
/// </summary>
public sealed class ProfileResourceObject : BaseResourceObject
{
    public const string ProfileJsonPropertyName = "profile";

    /// <summary>
    /// Gets or sets the Keyboard Manager remapping profile.
    /// </summary>
    [JsonPropertyName(ProfileJsonPropertyName)]
    [Required]
    [Description("The Keyboard Manager remapping profile.")]
    public KbmProfileModel Profile { get; set; } = new();
}
