// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.KeyboardManager.UITests;

internal sealed class KeyboardInputFixtureLock : IDisposable
{
    private static readonly string LockPath = Path.Combine(
        Path.GetTempPath(),
        "PowerToys.KeyboardManager.UITests.InputFixture.lock");

    private static readonly object Sync = new();
    private static FileStream? processLockFile;
    private static KeyboardManagerSettingsScope? settingsScope;
    private static int leaseCount;

    private bool disposed;

    private KeyboardInputFixtureLock()
    {
    }

    public static KeyboardInputFixtureLock Acquire()
    {
        lock (Sync)
        {
            if (processLockFile is not null)
            {
                leaseCount++;
                return new KeyboardInputFixtureLock();
            }

            var deadline = DateTime.UtcNow.AddMinutes(15);
            while (DateTime.UtcNow < deadline)
            {
                FileStream lockFile;
                try
                {
                    lockFile = new FileStream(
                        LockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                }
                catch (IOException exception) when (IsFileLockContention(exception))
                {
                    Thread.Sleep(200);
                    continue;
                }

                try
                {
                    settingsScope = new KeyboardManagerSettingsScope();
                    processLockFile = lockFile;
                    leaseCount = 1;
                    return new KeyboardInputFixtureLock();
                }
                catch
                {
                    lockFile.Dispose();
                    settingsScope = null;
                    throw;
                }
            }

            throw new TimeoutException("Another Keyboard Manager UI-test process retained the input fixture lock for 15 minutes.");
        }
    }

    private static bool IsFileLockContention(IOException exception)
    {
        int errorCode = exception.HResult & 0xFFFF;
        return errorCode is 32 or 33;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        lock (Sync)
        {
            disposed = true;
            leaseCount--;
            if (leaseCount == 0)
            {
                try
                {
                    settingsScope?.Dispose();
                }
                finally
                {
                    settingsScope = null;
                    processLockFile?.Dispose();
                    processLockFile = null;
                }
            }
        }
    }
}
