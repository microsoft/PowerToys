// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Policy;
using System.Text;
using System.Web;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.ValueGenerator.Uri
{
    public class UrlEncodeRequest : IComputeRequest
    {
        public byte[] Result { get; set; }

        public string Description => "Encoded URL";

        public bool IsSuccessful { get; set; }

        public string ErrorMessage { get; set; }

        private string DataToEncode { get; set; }

        public UrlEncodeRequest(string dataToEncode)
        {
            DataToEncode = dataToEncode ?? throw new ArgumentNullException(nameof(dataToEncode));
        }

        public bool Compute()
        {
            IsSuccessful = true;
            try
            {
                Result = Encoding.UTF8.GetBytes(HttpUtility.UrlEncode(DataToEncode));
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
