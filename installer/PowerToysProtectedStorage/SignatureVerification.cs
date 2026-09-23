// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PowerToys.ProtectedStorage.Build
{
    public static class SignatureVerification
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct VerifyParameters
        {
            public uint Size;
            public uint Encoding;
            public IntPtr Provider;
            public IntPtr GetSignerCertificate;
            public IntPtr GetArgument;
        }

        [DllImport("crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptVerifyDetachedMessageSignature(
            ref VerifyParameters parameters,
            uint signerIndex,
            byte[] signature,
            uint signatureSize,
            uint contentCount,
            IntPtr[] contents,
            uint[] contentSizes,
            out IntPtr signer);

        [DllImport("crypt32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CertFreeCertificateContext(IntPtr certificate);

        public static void Verify(byte[] content, byte[] signature)
        {
            var parameters = new VerifyParameters
            {
                Size = (uint)Marshal.SizeOf(typeof(VerifyParameters)),
                Encoding = 0x00010001,
            };
            var data = Marshal.AllocHGlobal(content.Length);
            IntPtr signer = IntPtr.Zero;
            try
            {
                Marshal.Copy(content, 0, data, content.Length);
                if (!CryptVerifyDetachedMessageSignature(
                    ref parameters, 0, signature, (uint)signature.Length, 1,
                    new[] { data }, new[] { (uint)content.Length }, out signer))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Native detached CMS verification failed.");
                }
            }
            finally
            {
                if (signer != IntPtr.Zero)
                {
                    CertFreeCertificateContext(signer);
                }

                Marshal.FreeHGlobal(data);
            }
        }
    }
}
