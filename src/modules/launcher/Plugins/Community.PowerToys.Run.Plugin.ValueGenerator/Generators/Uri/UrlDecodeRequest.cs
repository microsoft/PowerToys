// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Security.Policy;
using System.Text;
using System.Web;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.ValueGenerator.Uri
{
    public class UrlDecodeRequest : IComputeRequest
    {
        public byte[] Result { get; set; }

        public string Description => "Decoded URL";

        public bool IsSuccessful { get; set; }

        public string ErrorMessage { get; set; }

        private string DataToDecode { get; set; }

        public UrlDecodeRequest(string dataToDecode)
        {
            DataToDecode = dataToDecode ?? throw new ArgumentNullException(nameof(dataToDecode));
        }

        public bool Compute()
        {
            IsSuccessful = true;
            try
            {
                Result = Encoding.UTF8.GetBytes(HttpUtility.UrlDecode(DataToDecode));
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
