// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization.Metadata;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Provides AOT-compatible JSON file persistence with shallow-merge strategy.
/// </summary>
public interface IPersistenceService
{
    /// <summary>
    /// Loads and deserializes a model from the specified JSON file.
    /// Returns a new <typeparamref name="T"/> instance when the file is missing or unreadable.
    /// </summary>
    T Load<T>(string filePath, JsonTypeInfo<T> typeInfo)
        where T : new();

    /// <summary>
    /// Serializes <paramref name="model"/>, shallow-merges into the existing file
    /// (preserving unknown keys), and writes the result back to disk.
    /// </summary>
    /// <param name="model">The model to persist.</param>
    /// <param name="filePath">Target JSON file path.</param>
    /// <param name="typeInfo">AOT-compatible type metadata.</param>
    void Save<T>(T model, string filePath, JsonTypeInfo<T> typeInfo);
}
