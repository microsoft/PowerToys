// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Logger infrastructure uses ManagedCommon.Logger static class pattern", Scope = "namespaceanddescendants", Target = "~N:Microsoft.CmdPal.UI.ViewModels")]
[assembly: SuppressMessage("Performance", "CA1835:Prefer 'Memory'-based 'ReadAsync' and 'WriteAsync' overloads", Justification = "Stream-based APIs are used for compatibility", Scope = "namespaceanddescendants", Target = "~N:Microsoft.CmdPal.UI.ViewModels")]
[assembly: SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Inline arrays improve readability in specific contexts", Scope = "namespaceanddescendants", Target = "~N:Microsoft.CmdPal.UI.ViewModels")]
[assembly: SuppressMessage("Performance", "CA1513:Use ObjectDisposedException.ThrowIf", Justification = "Compatibility with existing patterns", Scope = "namespaceanddescendants", Target = "~N:Microsoft.CmdPal.UI.ViewModels")]
[assembly: SuppressMessage("Interoperability", "CsWinRT1028:Class implements WinRT interfaces but isn't marked partial", Justification = "Partial classes not required for non-WinRT types", Scope = "namespaceanddescendants", Target = "~N:Microsoft.CmdPal.UI.ViewModels")]
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1402:File may only contain a single type", Justification = "Multiple related types in single file for clarity", Scope = "namespaceanddescendants", Target = "~N:Microsoft.CmdPal.UI.ViewModels")]
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649:File name should match first type name", Justification = "File name represents concept, not first type", Scope = "namespaceanddescendants", Target = "~N:Microsoft.CmdPal.UI.ViewModels")]
