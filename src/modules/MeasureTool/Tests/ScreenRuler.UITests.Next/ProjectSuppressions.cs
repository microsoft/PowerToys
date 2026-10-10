// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Scope = "namespace",
    Target = "~N:ScreenRuler.UITests.Next",
    Justification = "The .Next suffix identifies tests built on UITestAutomation.Next; this test-only assembly is never consumed from other languages.")]
