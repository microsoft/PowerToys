// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.Services.PythonScripts;

/// <summary>
/// Represents a single Python package requirement declared via
/// <c># @advancedpaste:requires import_name=pip_package</c>.
/// </summary>
/// <param name="ImportName">The Python import name used in the script (e.g. "cv2").</param>
/// <param name="PipPackage">The pip install name (e.g. "opencv-python-headless"). Equals <see cref="ImportName"/> when not explicitly specified.</param>
public sealed record PythonRequirement(string ImportName, string PipPackage);
