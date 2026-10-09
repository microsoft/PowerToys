// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

// Enable compile-time marshalling for all P/Invoke declarations
// This allows LibraryImport to handle array marshalling and achieve 100% coverage
[assembly: DisableRuntimeMarshalling]
