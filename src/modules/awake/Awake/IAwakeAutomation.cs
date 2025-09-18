// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Awake
{
    /// <summary>
    /// COM automation interface exposed via ROT for controlling Awake.
    /// </summary>
    [ComVisible(true)]
    [Guid("5CA92C1D-9D7E-4F6D-9B06-5B7B28BF4E21")]
    public interface IAwakeAutomation
    {
        string Ping();

        void SetIndefinite();

        void SetTimed(int seconds);

        void SetExpirable(int minutes);

        void SetPassive();

        void Cancel();

        string GetStatusJson();
    }
}
