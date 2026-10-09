// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Settings.UI.Library.Attributes;

/// <summary>
/// Adding this attribute to a property makes it not configurable from the command line.
/// Typical use cases:
/// - Property represents internal module state.
/// - Property has a type that is unwieldy to type as a command line string.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class CmdConfigureIgnoreAttribute : Attribute;
