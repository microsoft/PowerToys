#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

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

