// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Metadata information for an AI service type.
    /// </summary>
    public class AIServiceTypeMetadata
    {
        public AIServiceType ServiceType { get; init; }

        public string DisplayName { get; init; }

        public string IconPath { get; init; }

        public bool IsOnlineService { get; init; }

        public bool IsAvailableInUI { get; init; } = true;

        public bool IsLocalModel { get; init; }

        public string LegalDescription { get; init; }

        public string TermsLabel { get; init; }

        public Uri TermsUri { get; init; }

        public string PrivacyLabel { get; init; }

        public Uri PrivacyUri { get; init; }

        public bool HasLegalInfo => !string.IsNullOrWhiteSpace(LegalDescription);

        public bool HasTermsLink => TermsUri is not null && !string.IsNullOrEmpty(TermsLabel);

        public bool HasPrivacyLink => PrivacyUri is not null && !string.IsNullOrEmpty(PrivacyLabel);
    }
}
