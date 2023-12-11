// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1009:ClosingParenthesisMustBeSpacedCorrectly", Justification = "All current violations are due to Tuple shorthand and so valid.")]

[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1101:PrefixLocalCallsWithThis", Justification = "We follow the C# Core Coding Style which avoids using `this` unless absolutely necessary.")]

[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1200:UsingDirectivesMustBePlacedWithinNamespace", Justification = "We follow the C# Core Coding Style which puts using statements outside the namespace.")]
[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:ElementsMustAppearInTheCorrectOrder", Justification = "It is not a priority and has a high impact in code changes.")]
[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1202:ElementsMustBeOrderedByAccess", Justification = "It is not a priority and has a high impact in code changes.")]
[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1203:ConstantsMustAppearBeforeFields", Justification = "It is not a priority and has a high impact in code changes.")]
[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1204:StaticElementsMustAppearBeforeInstanceElements", Justification = "It is not a priority and has a high impact in code changes.")]

[assembly: SuppressMessage("StyleCop.CSharp.NamingRules", "SA1309:FieldNamesMustNotBeginWithUnderscore", Justification = "We follow the C# Core Coding Style which uses underscores as prefixes rather than using `this.`.")]

[assembly: SuppressMessage("StyleCop.CSharp.SpecialRules", "SA0001:XmlCommentAnalysisDisabled", Justification = "Not enabled as we don't want or need XML documentation.")]
[assembly: SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1629:DocumentationTextMustEndWithAPeriod", Justification = "Not enabled as we don't want or need XML documentation.")]

[assembly: SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly", Scope = "member", Target = "Microsoft.Templates.Core.Locations.TemplatesSynchronization.#SyncStatusChanged", Justification = "Using an Action<object, SyncStatusEventArgs> does not allow the required notation")]

// Non general suppressions
[assembly: SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "This is part of the markdown processing", MessageId = "System.Windows.Documents.Run.#ctor(System.String)", Scope = "member", Target = "Microsoft.Templates.UI.Controls.Markdown.#ImageInlineEvaluator(System.Text.RegularExpressions.Match)")]
[assembly: SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "We need to have the names of these keys in lowercase to be able to compare with the keys becoming form the template json. ContainsKey does not allow StringComparer specification to IgnoreCase", Scope = "member", Target = "Microsoft.Templates.Core.ITemplateInfoExtensions.#GetQueryableProperties(Microsoft.TemplateEngine.Abstractions.ITemplateInfo)")]
[assembly: SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "We need to have the names of these keys in lowercase to be able to compare with the keys becoming form the template json. ContainsKey does not allow StringComparer specification to IgnoreCase", Scope = "member", Target = "Microsoft.Templates.Core.Composition.CompositionQuery.#Match(System.Collections.Generic.IEnumerable`1<Microsoft.Templates.Core.Composition.QueryNode>,Microsoft.Templates.Core.Composition.QueryablePropertyDictionary)")]
[assembly: SuppressMessage("Usage", "VSTHRD103:Call async methods when in an async method", Justification = "Resource DictionaryWriter does not implement flush async", Scope = "member", Target = "~M:Microsoft.Templates.Core.PostActions.Catalog.Merge.MergeResourceDictionaryPostAction.ExecuteInternalAsync~System.Threading.Tasks.Task")]

// Threading suppressions
[assembly: SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD100:Avoid async void methods", Justification = "Event handlers needs async void", Scope = "member", Target = "~M:Microsoft.Templates.UI.Controls.Notification.OnClose")]
[assembly: SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD100:Avoid async void methods", Justification = "Event handlers needs async void", Scope = "member", Target = "~M:Microsoft.Templates.UI.ViewModels.Common.SavedTemplateViewModel.OnDelete")]
[assembly: SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD100:Avoid async void methods", Justification = "Event handlers needs async void", Scope = "member", Target = "~M:Microsoft.Templates.UI.ViewModels.Common.WizardNavigation.GoBack")]
[assembly: SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD100:Avoid async void methods", Justification = "Event handlers needs async void", Scope = "member", Target = "~M:Microsoft.Templates.UI.ViewModels.Common.WizardNavigation.GoForward")]
[assembly: SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD100:Avoid async void methods", Justification = "Event handlers needs async void", Scope = "member", Target = "~M:Microsoft.Templates.UI.ViewModels.Common.SavedTemplateViewModel.OnDelete(Microsoft.Templates.UI.ViewModels.Common.SavedTemplateViewModel)")]

// Localization suppressions
[assembly: SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Templates.Core.Locations.JunctionNativeMethods.ThrowLastWin32Error(System.String)", Scope = "member", Target = "Microsoft.Templates.Core.Locations.JunctionNativeMethods.#CreateJunction(System.String,System.String,System.Boolean)", Justification = "Only used for local generation")]
[assembly: SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Templates.Core.Locations.JunctionNativeMethods.ThrowLastWin32Error(System.String)", Scope = "member", Target = "Microsoft.Templates.Core.Locations.JunctionNativeMethods.#DeleteJunction(System.String)", Justification = "Only used for local generation")]
[assembly: SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Templates.Core.Locations.JunctionNativeMethods.ThrowLastWin32Error(System.String)", Scope = "member", Target = "Microsoft.Templates.Core.Locations.JunctionNativeMethods.#InternalGetTarget(Microsoft.Win32.SafeHandles.SafeFileHandle)", Justification = "Only used for local generation")]
[assembly: SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Templates.Core.Locations.JunctionNativeMethods.ThrowLastWin32Error(System.String)", Scope = "member", Target = "Microsoft.Templates.Core.Locations.JunctionNativeMethods.#OpenReparsePoint(System.String,Microsoft.Templates.Core.Locations.JunctionNativeMethods+EFileAccess)", Justification = "Only used for local generation")]
[assembly: SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "System.Windows.Documents.InlineCollection.Add(System.String)", Scope = "member", Target = "Microsoft.Templates.UI.Extensions.TextBlockExtensions.#OnSequentialFlowStepChanged(System.Windows.DependencyObject,System.Windows.DependencyPropertyChangedEventArgs)", Justification = "No text here")]
