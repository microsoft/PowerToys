// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TopToolbar.Models;
using TopToolbar.Models.Abstractions;

namespace TopToolbar.Services.Profiles
{
    /// <summary>
    /// Abstraction for runtime profile state: active profile selection, loading and change notification.
    /// Keeps ProfileFileService usage out of UI surfaces.
    /// </summary>
    public interface IProfileRuntime : IDisposable
    {
        /// <summary>Gets currently active profile identifier.</summary>
        string ActiveProfileId { get; }

        /// <summary>Gets the currently active loaded profile (may be null before initialization).</summary>
        IProfile ActiveProfile { get; }

        /// <summary>Raised after the active profile object is loaded or switched.</summary>
        event EventHandler<IProfile> ActiveProfileChanged;

        /// <summary>Initialize runtime: load or create initial active profile.</summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>Attempt to switch to another profile id. Returns false if not found or same id.</summary>
        bool Switch(string profileId);

        /// <summary>Notify that the current active profile has been updated externally (e.g., from settings UI).</summary>
        void NotifyActiveProfileUpdated();

        /// <summary>Returns all profiles (pure read of current files).</summary>
        IReadOnlyList<IProfile> GetAllProfiles();

        /// <summary>Gets underlying file service for advanced operations (avoid in UI where possible).</summary>
        ProfileFileService FileService { get; }
    }
}
