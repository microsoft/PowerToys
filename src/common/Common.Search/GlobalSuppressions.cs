// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("StyleCop.CSharp.NamingRules", "SA1309:Field names should not begin with underscore", Justification = "coding style", Scope = "member", Target = "~F:Common.Search.MatchResult._rawScore")]
[assembly: SuppressMessage("StyleCop.CSharp.NamingRules", "SA1309:Field names should not begin with underscore", Justification = "coding style", Scope = "member", Target = "~F:Common.Search.StringMatcher._defaultMatchOption")]
[assembly: SuppressMessage("StyleCop.CSharp.NamingRules", "SA1309:Field names should not begin with underscore", Justification = "coding style", Scope = "member", Target = "~F:Common.Search.StringMatcher._instance")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1101:Prefix local calls with this", Justification = "coding style", Scope = "member", Target = "~M:Common.Search.MatchResult.#ctor(System.Boolean,Common.Search.SearchPrecisionScore)")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1101:Prefix local calls with this", Justification = "coding style", Scope = "member", Target = "~M:Common.Search.MatchResult.#ctor(System.Boolean,Common.Search.SearchPrecisionScore,System.Collections.Generic.List{System.Int32},System.Int32)")]
[assembly: SuppressMessage("Compiler", "CS8618:Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.", Justification = "Coding style", Scope = "member", Target = "~F:Common.Search.StringMatcher._instance")]
[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:Elements should appear in the correct order", Justification = "coding style", Scope = "member", Target = "~F:Common.Search.StringMatcher._instance")]
[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:Elements should appear in the correct order", Justification = "coding style", Scope = "member", Target = "~F:Common.Search.StringMatcher.Separator")]
[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:Elements should appear in the correct order", Justification = "coding style", Scope = "member", Target = "~M:Common.Search.StringMatcher.#ctor")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1101:Prefix local calls with this", Justification = "coding style", Scope = "member", Target = "~M:Common.Search.StringMatcher.FuzzyMatch(System.String,System.String)~Common.Search.MatchResult")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1101:Prefix local calls with this", Justification = "coding style", Scope = "member", Target = "~M:Common.Search.StringMatcher.FuzzyMatch(System.String,System.String,Common.Search.MatchOption)~Common.Search.MatchResult")]
[assembly: SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1407:Arithmetic expressions should declare precedence", Justification = "migrate from stable code", Scope = "member", Target = "~M:Common.Search.StringMatcher.CalculateSearchScore(System.String,System.String,System.Int32,System.Int32,System.Boolean)~System.Int32")]
[assembly: SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1101:Prefix local calls with this", Justification = "migrate from stable code", Scope = "member", Target = "~M:Common.Search.StringMatcher.FuzzyMatch(System.String,System.String,Common.Search.MatchOption,System.Int32)~Common.Search.MatchResult")]
[assembly: SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1204:Static elements should appear before instance elements", Justification = "migrate from stable code", Scope = "member", Target = "~M:Common.Search.StringMatcher.CalculateClosestSpaceIndex(System.Collections.Generic.List{System.Int32},System.Int32)~System.Int32")]
