// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "'Next' (VB keyword) is part of the established Microsoft.PowerToys.UITest.Next namespace consumed by every *.UITests.Next project; renaming it is a breaking change and the framework is only consumed from C#.", Scope = "namespace", Target = "~N:Microsoft.PowerToys.UITest.Next")]
