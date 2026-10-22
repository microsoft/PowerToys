// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public sealed class AtomicSettingsPublicationException : IOException
    {
        public AtomicSettingsPublicationException(
            AtomicSettingsPublicationOutcome outcome,
            string settingsPath,
            IReadOnlyList<string>? recoveryPaths,
            Exception publicationException,
            Exception? compensationException = null)
            : base(CreateMessage(outcome, settingsPath, recoveryPaths), CreateInnerException(publicationException, compensationException))
        {
            Outcome = outcome;
            SettingsPath = settingsPath;
            RecoveryPaths = recoveryPaths?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
            PublicationException = publicationException;
            CompensationException = compensationException;
        }

        public AtomicSettingsPublicationOutcome Outcome { get; }

        public string SettingsPath { get; }

        public IReadOnlyList<string> RecoveryPaths { get; }

        public Exception PublicationException { get; }

        public Exception? CompensationException { get; }

        private static Exception CreateInnerException(Exception publicationException, Exception? compensationException)
            => compensationException is null
                ? publicationException
                : new AggregateException(publicationException, compensationException);

        private static string CreateMessage(
            AtomicSettingsPublicationOutcome outcome,
            string settingsPath,
            IReadOnlyList<string>? recoveryPaths)
        {
            string recovery = recoveryPaths is null
                ? string.Empty
                : string.Join(", ", recoveryPaths.Where(path => !string.IsNullOrWhiteSpace(path)).Select(path => $"'{path}'"));
            string recoverySuffix = string.IsNullOrEmpty(recovery) ? string.Empty : $" Recovery file(s): {recovery}.";
            return outcome switch
            {
                AtomicSettingsPublicationOutcome.PreviousSettingsVerified =>
                    $"Settings publication failed, and the previous settings at '{settingsPath}' were verified intact or restored.{recoverySuffix}",
                AtomicSettingsPublicationOutcome.NewSettingsVerified =>
                    $"Settings publication reported an error, but the new settings at '{settingsPath}' were verified committed.{recoverySuffix}",
                _ =>
                    $"Settings publication failed, and the state of '{settingsPath}' could not be verified.{recoverySuffix}",
            };
        }
    }
}
