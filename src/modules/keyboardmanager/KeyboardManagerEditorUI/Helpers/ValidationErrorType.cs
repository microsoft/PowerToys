// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyboardManagerEditorUI.Helpers
{
    public enum ValidationErrorType
    {
        NoError,
        EmptyOriginalKeys,
        EmptyRemappedKeys,
        ModifierOnly,
        EmptyAppName,
        IllegalShortcut,
        DuplicateMapping,
        SelfMapping,
        EmptyTargetText,
        OneKeyMapping,
        DuplicateMouseMapping,
        EmptyMouseActionKeys,
        EmptyMouseTargetText,
        EmptyMouseUrl,
        EmptyMouseProgramPath,
        EmptyMouseTargetButton,
        MouseAppNameMissing,
        MouseSelfMapping,
    }
}
