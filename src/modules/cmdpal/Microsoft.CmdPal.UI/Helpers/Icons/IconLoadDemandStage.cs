// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal enum IconLoadDemandStage
{
    // These values are emitted in the RequestInvalidated ETW payload. Preserve
    // existing values and append new stages so saved traces remain decodable.
    Unlinked = 0,
    BeforeEnqueue = 1,
    Queued = 2,
    WorkerActive = 3,
    Completed = 4,
    Rejected = 5,
    Abandoned = 6,
    AwaitingSharedLoad = 7,
}
