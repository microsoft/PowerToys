// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace MouseWithoutBorders.Class;

internal sealed class DpapiPairingKeyCodec : PairingKeyCodec
{
    private const string ProtectionDescriptor = "LOCAL=user";

    // Values from the public Windows SDK ncryptprotect.h and ncrypt.h.
    private const uint UnprotectNoDecrypt = 0x00000001;
    private const uint DescriptorStringInfo = 0x00000001;
    private const uint SilentFlag = 0x00000040;

    private readonly byte[] _interactiveUserSid;

    internal DpapiPairingKeyCodec(SecurityIdentifier interactiveUserSid)
    {
        ArgumentNullException.ThrowIfNull(interactiveUserSid);
        _interactiveUserSid = new byte[interactiveUserSid.BinaryLength];
        interactiveUserSid.GetBinaryForm(_interactiveUserSid, 0);
    }

    internal static IPairingKeyCodec CreateForCurrentIdentity(SecurityIdentifier interactiveUserSid)
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.IsSystem ? new DpapiPairingKeyCodec(interactiveUserSid) : new PassthroughPairingKeyCodec();
    }

    public override string Encrypt(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (IsEncrypted(value))
        {
            throw new ArgumentException("Expected an unencrypted pairing key.", nameof(value));
        }

        // A one-byte SID length, binary SID, and UTF-8 key are protected together.
        var plaintext = new byte[1 + _interactiveUserSid.Length + Encoding.UTF8.GetByteCount(value)];
        plaintext[0] = checked((byte)_interactiveUserSid.Length);
        _interactiveUserSid.CopyTo(plaintext, 1);
        Encoding.UTF8.GetBytes(value, plaintext.AsSpan(1 + _interactiveUserSid.Length));
        IntPtr descriptor = IntPtr.Zero;
        IntPtr protectedBlob = IntPtr.Zero;
        try
        {
            CheckStatus(NativeMethods.NCryptCreateProtectionDescriptor(ProtectionDescriptor, 0, out descriptor));
            CheckStatus(NativeMethods.NCryptProtectSecret(
                descriptor,
                SilentFlag,
                plaintext,
                checked((uint)plaintext.Length),
                IntPtr.Zero,
                IntPtr.Zero,
                out protectedBlob,
                out var protectedLength));
            var encrypted = new byte[checked((int)protectedLength)];
            Marshal.Copy(protectedBlob, encrypted, 0, encrypted.Length);
            return EncryptedPrefix + EncryptedVersion +
                Convert.ToBase64String(encrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (protectedBlob != IntPtr.Zero)
            {
                NativeMethods.LocalFree(protectedBlob);
            }

            if (descriptor != IntPtr.Zero)
            {
                _ = NativeMethods.NCryptCloseProtectionDescriptor(descriptor);
            }
        }
    }

    public override string Decrypt(string value)
    {
        const string prefix = EncryptedPrefix + EncryptedVersion;
        if (value?.StartsWith(prefix, StringComparison.Ordinal) != true)
        {
            throw new CryptographicException("Missing or unsupported encrypted pairing key prefix.");
        }

        byte[] encrypted;
        try
        {
            encrypted = Convert.FromBase64String(value.Substring(prefix.Length));
        }
        catch (FormatException exception)
        {
            throw new CryptographicException("Malformed encrypted pairing key payload.", exception);
        }

        if (encrypted.Length == 0)
        {
            throw new CryptographicException("Missing encrypted pairing key payload.");
        }

        ValidateProtectionPolicy(encrypted);
        IntPtr descriptor = IntPtr.Zero;
        IntPtr nativePlaintext = IntPtr.Zero;
        uint plaintextLength = 0;
        byte[] plaintext = null;
        try
        {
            CheckStatus(NativeMethods.NCryptUnprotectSecret(
                out descriptor,
                SilentFlag,
                encrypted,
                checked((uint)encrypted.Length),
                IntPtr.Zero,
                IntPtr.Zero,
                out nativePlaintext,
                out plaintextLength));
            plaintext = new byte[checked((int)plaintextLength)];
            Marshal.Copy(nativePlaintext, plaintext, 0, plaintext.Length);
            if (plaintext.Length <= 1 + _interactiveUserSid.Length ||
                plaintext[0] != _interactiveUserSid.Length ||
                !CryptographicOperations.FixedTimeEquals(plaintext.AsSpan(1, _interactiveUserSid.Length), _interactiveUserSid))
            {
                throw new CryptographicException("Invalid protected pairing key owner.");
            }

            var key = new UTF8Encoding(false, true).GetString(plaintext.AsSpan(1 + _interactiveUserSid.Length));
            if (string.IsNullOrWhiteSpace(key) || IsEncrypted(key))
            {
                throw new CryptographicException("Invalid protected pairing key.");
            }

            return key;
        }
        catch (DecoderFallbackException exception)
        {
            throw new CryptographicException("Invalid protected pairing key.", exception);
        }
        finally
        {
            if (plaintext != null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            ZeroAndFree(nativePlaintext, plaintextLength);
            if (descriptor != IntPtr.Zero)
            {
                _ = NativeMethods.NCryptCloseProtectionDescriptor(descriptor);
            }
        }
    }

    private static void ValidateProtectionPolicy(byte[] encrypted)
    {
        IntPtr descriptor = IntPtr.Zero;
        IntPtr descriptorString = IntPtr.Zero;
        IntPtr unusedData = IntPtr.Zero;
        uint unusedLength = 0;
        try
        {
            // Inspect the descriptor through the documented API before decrypting any payload.
            CheckStatus(NativeMethods.NCryptUnprotectSecret(
                out descriptor,
                UnprotectNoDecrypt | SilentFlag,
                encrypted,
                checked((uint)encrypted.Length),
                IntPtr.Zero,
                IntPtr.Zero,
                out unusedData,
                out unusedLength));
            CheckStatus(NativeMethods.NCryptGetProtectionDescriptorInfo(
                descriptor, IntPtr.Zero, DescriptorStringInfo, out descriptorString));
            if (!string.Equals(Marshal.PtrToStringUni(descriptorString), ProtectionDescriptor, StringComparison.Ordinal))
            {
                throw new CryptographicException("Invalid pairing key protection descriptor.");
            }
        }
        finally
        {
            ZeroAndFree(unusedData, unusedLength);
            if (descriptorString != IntPtr.Zero)
            {
                NativeMethods.LocalFree(descriptorString);
            }

            if (descriptor != IntPtr.Zero)
            {
                _ = NativeMethods.NCryptCloseProtectionDescriptor(descriptor);
            }
        }
    }

    private static void CheckStatus(int status)
    {
        if (status != 0)
        {
            throw new CryptographicException(status);
        }
    }

    private static void ZeroAndFree(IntPtr data, uint length)
    {
        if (data == IntPtr.Zero)
        {
            return;
        }

        try
        {
            for (uint offset = 0; offset < length; offset++)
            {
                Marshal.WriteByte(IntPtr.Add(data, checked((int)offset)), 0);
            }
        }
        finally
        {
            NativeMethods.LocalFree(data);
        }
    }

    private static class NativeMethods
    {
        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern int NCryptCreateProtectionDescriptor(string descriptorString, uint flags, out IntPtr descriptor);

        [DllImport("ncrypt.dll", ExactSpelling = true)]
        internal static extern int NCryptCloseProtectionDescriptor(IntPtr descriptor);

        [DllImport("ncrypt.dll", ExactSpelling = true)]
        internal static extern int NCryptProtectSecret(
            IntPtr descriptor,
            uint flags,
            [In] byte[] data,
            uint dataLength,
            IntPtr allocationParameters,
            IntPtr window,
            out IntPtr protectedBlob,
            out uint protectedLength);

        [DllImport("ncrypt.dll", ExactSpelling = true)]
        internal static extern int NCryptUnprotectSecret(
            out IntPtr descriptor,
            uint flags,
            [In] byte[] protectedBlob,
            uint protectedLength,
            IntPtr allocationParameters,
            IntPtr window,
            out IntPtr data,
            out uint dataLength);

        [DllImport("ncrypt.dll", ExactSpelling = true)]
        internal static extern int NCryptGetProtectionDescriptorInfo(
            IntPtr descriptor, IntPtr allocationParameters, uint infoType, out IntPtr info);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        internal static extern IntPtr LocalFree(IntPtr memory);
    }
}
