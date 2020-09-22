// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ManagedCommon
{
    public static class ApplicationTitle
    {
        public static string DevEnvironment
        {
            get
            {
                if (!string.IsNullOrEmpty(ManagedCommon.DevEnvironment.Environment))
                {
                    return string.Format(CultureInfo.CurrentCulture, "[{0}]", ManagedCommon.DevEnvironment.Environment);
                }

                return string.Empty;
            }
        }
    }
}
