// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.ValueGenerator.Base64
{
    public class Base64Request : IComputeRequest
    {
        public byte[] Result { get; set; }

        public string Description => "Base64 Encoding";

        public bool IsSuccessful { get; set; }

        public string ErrorMessage { get; set; }

        private byte[] DataToEncode { get; set; }

        public Base64Request(byte[] dataToEncode)
        {
            DataToEncode = dataToEncode ?? throw new ArgumentNullException(nameof(dataToEncode));
        }

        public bool Compute()
        {
            IsSuccessful = true;
            try
            {
                Result = Encoding.UTF8.GetBytes(System.Convert.ToBase64String(DataToEncode));
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
