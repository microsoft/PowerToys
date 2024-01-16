// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.ValueGenerator.Uri
{
    public class DataEscapeRequest : IComputeRequest
    {
        public byte[] Result { get; set; }

        public string Description => "Data string escaped";

        public bool IsSuccessful { get; set; }

        public string ErrorMessage { get; set; }

        private string DataToEscape { get; set; }

        public DataEscapeRequest(string dataToEscape)
        {
            DataToEscape = dataToEscape ?? throw new ArgumentNullException(nameof(dataToEscape));
        }

        public bool Compute()
        {
            IsSuccessful = true;
            try
            {
                Result = Encoding.UTF8.GetBytes(System.Uri.EscapeDataString(DataToEscape));
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
            return Encoding.UTF8.GetString(Result);
        }
    }
}
