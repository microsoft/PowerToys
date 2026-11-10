// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.FileLocksmith.UITests;

/// <summary>One stable look at an open context menu.</summary>
internal sealed record MenuObservation(bool IsOpen, bool HasCommand, bool HasSibling);
