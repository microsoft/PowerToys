// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Text;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.ValueGenerator.Uri
{
    public class DataUnescapeRequest : IComputeRequest
    {
        public byte[] Result { get; set; }

        public string Description => "Data string unescaped";

        public bool IsSuccessful { get; set; }

        public string ErrorMessage { get; set; }

        private string DataToUnescape { get; set; }

        public DataUnescapeRequest(string dataToUnescape)
        {
            DataToUnescape = dataToUnescape ?? throw new ArgumentNullException(nameof(dataToUnescape));
        }

        public bool Compute()
        {
            IsSuccessful = true;
            try
            {
                Result = Encoding.UTF8.GetBytes(System.Uri.UnescapeDataString(DataToUnescape));
            }
            catch (Exception e)
            {
                Log.Exception(e.Message, e, GetType());
                ErrorMessage = e.Message;
                IsSuccessful = false;
            }

            return IsSuccessful;
        }

        public string ResultToString()
        {
            if (Result != null)
            {
                return Encoding.UTF8.GetString(Result);
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
