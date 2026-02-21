// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.GPOWrapper;

namespace RunnerV2.Models
{
    public interface IPowerToysModule
    {
        /// <summary>
        /// Gets the short name of the module. The same used as the name of the folder containing its settings.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// This function is called when the module is enabled.
        /// </summary>
        public void Enable();

        /// <summary>
        /// This function is called when the module is disabled.
        /// </summary>
        public void Disable();

        /// <summary>
        /// Gets a value indicating whether the module is enabled.
        /// </summary>
        /// <remarks>
        /// This value shall be read from the settings of the module in the module interface implementation.
        /// </remarks>
        public bool Enabled { get; }

        /// <summary>
        /// Gets the GPO rule configured state for the module.
        /// </summary>
        /// <remarks>
        /// This value shall be read from the GPO settings with the <see cref="GPOWrapper"/> class.
        /// </remarks>
        public GpoRuleConfigured GpoRuleConfigured { get; }
    }
}
