// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Workspaces.SignatureFixtures
{
    public static class FixtureSigner
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SignerFileInfo
        {
            public uint Size;
            public IntPtr FileName;
            public IntPtr File;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SignerSubjectInfo
        {
            public uint Size;
            public IntPtr Index;
            public uint SubjectChoice;
            public IntPtr FileInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SignerCertStoreInfo
        {
            public uint Size;
            public IntPtr SigningCert;
            public uint CertPolicy;
            public IntPtr Store;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SignerCert
        {
            public uint Size;
            public uint CertChoice;
            public IntPtr StoreInfo;
            public IntPtr Window;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SignerSignatureInfo
        {
            public uint Size;
            public uint HashAlgorithm;
            public uint AttributeChoice;
            public IntPtr AuthenticodeAttributes;
            public IntPtr AuthenticatedAttributes;
            public IntPtr UnauthenticatedAttributes;
        }

        [DllImport("mssign32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int SignerSignEx2(
            uint flags, ref SignerSubjectInfo subject, ref SignerCert certificate,
            ref SignerSignatureInfo signature, IntPtr provider, uint timestampFlags,
            IntPtr timestampAlgorithm, IntPtr timestampUrl, IntPtr request,
            IntPtr sipData, out IntPtr signerContext, IntPtr cryptoPolicy, IntPtr reserved);

        [DllImport("mssign32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int SignerFreeSignerContext(IntPtr context);

        [DllImport("crypt32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr CertOpenStore(IntPtr provider, uint encoding, IntPtr cryptProvider, uint flags, IntPtr parameter);

        [DllImport("crypt32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CertAddCertificateContextToStore(IntPtr store, IntPtr certificate, uint disposition, IntPtr storeContext);

        [DllImport("crypt32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CertCloseStore(IntPtr store, uint flags);

        private static IntPtr Allocate<T>(T value, List<IntPtr> allocations)
            where T : struct
        {
            IntPtr address = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
            allocations.Add(address);
            Marshal.StructureToPtr(value, address, false);
            return address;
        }

        public static void Sign(string path, X509Certificate2 certificate)
        {
            if (!certificate.HasPrivateKey)
            {
                throw new ArgumentException("The ephemeral test certificate has no private key.", nameof(certificate));
            }

            var allocations = new List<IntPtr>();
            IntPtr context = IntPtr.Zero;
            // CERT_STORE_PROV_MEMORY: no persistent certificate or trust store is opened.
            IntPtr store = CertOpenStore(new IntPtr(2), 0, IntPtr.Zero, 0, IntPtr.Zero);
            if (store == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                if (!CertAddCertificateContextToStore(store, certificate.Handle, 4, IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                IntPtr name = Marshal.StringToHGlobalUni(path);
                allocations.Add(name);
                var file = new SignerFileInfo
                {
                    Size = (uint)Marshal.SizeOf<SignerFileInfo>(),
                    FileName = name,
                };
                var subject = new SignerSubjectInfo
                {
                    Size = (uint)Marshal.SizeOf<SignerSubjectInfo>(),
                    Index = Allocate(0u, allocations),
                    SubjectChoice = 1,
                    FileInfo = Allocate(file, allocations),
                };
                var storeInfo = new SignerCertStoreInfo
                {
                    Size = (uint)Marshal.SizeOf<SignerCertStoreInfo>(),
                    SigningCert = certificate.Handle,
                    CertPolicy = 1,
                    Store = store,
                };
                var signingCert = new SignerCert
                {
                    Size = (uint)Marshal.SizeOf<SignerCert>(),
                    CertChoice = 2,
                    StoreInfo = Allocate(storeInfo, allocations),
                };
                var signature = new SignerSignatureInfo
                {
                    Size = (uint)Marshal.SizeOf<SignerSignatureInfo>(),
                    HashAlgorithm = 0x800c,
                };

                int result = SignerSignEx2(
                    0, ref subject, ref signingCert, ref signature, IntPtr.Zero, 0,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out context,
                    IntPtr.Zero, IntPtr.Zero);
                if (result != 0)
                {
                    throw new COMException($"SignerSignEx2 failed: 0x{unchecked((uint)result):X8}", result);
                }
            }
            finally
            {
                if (context != IntPtr.Zero)
                {
                    SignerFreeSignerContext(context);
                }

                foreach (IntPtr allocation in allocations)
                {
                    Marshal.FreeHGlobal(allocation);
                }

                CertCloseStore(store, 0);
                GC.KeepAlive(certificate);
            }
        }
    }
}
