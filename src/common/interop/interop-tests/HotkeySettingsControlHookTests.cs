// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.Interop;

namespace Microsoft.Interop.Tests
{
    [TestClass]
    public class HotkeySettingsControlHookTests
    {
        [TestMethod]
        [DataRow(0x100, 1, 0)]
        [DataRow(0x104, 1, 0)]
        [DataRow(0x101, 0, 1)]
        [DataRow(0x105, 0, 1)]
        [DataRow(0x102, 0, 0)]
        public void CallbackPreservesKeyDispatch(int message, int keyDownCount, int keyUpCount)
        {
            var owner = new CallbackOwner();
            using var hook = CreateHook(owner, out var native);
            native.KeyboardEvent(new KeyboardEvent { message = (ulong)message, key = 65 });

            Assert.AreEqual(keyDownCount, owner.KeyDownCount);
            Assert.AreEqual(keyUpCount, owner.KeyUpCount);
            Assert.AreEqual(keyDownCount + keyUpCount == 0 ? 0 : 65, owner.LastKey);
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void CallbacksPreserveActiveAndFilterResults(bool active, bool filter)
        {
            var owner = new CallbackOwner { Active = active, Filter = filter };
            using var hook = CreateHook(owner, out var native);
            const ulong extraInfo = 0x100000042;

            Assert.AreEqual(active, native.IsActive());
            Assert.AreEqual(filter, native.Filter(new KeyboardEvent { key = 65, dwExtraInfo = extraInfo }));
            Assert.AreEqual(65, owner.LastKey);
            Assert.AreEqual(extraInfo, owner.LastExtraInfo.ToUInt64());
            Assert.IsFalse(hook.GetDisposedState());
        }

        [TestMethod]
        public void DisposeIsIdempotentAndReentrantWithInactiveLateCallbacks()
        {
            var owner = new CallbackOwner();
            using var hook = CreateHook(owner, out var native);
            native.Closing = () =>
            {
                AssertCallbacksInactive(native);
                hook.Dispose();
            };

            hook.Dispose();
            hook.Dispose();
            AssertCallbacksInactive(native);

            Assert.IsTrue(hook.GetDisposedState());
            Assert.AreEqual(1, native.DisposeCount);
            Assert.AreEqual(0, owner.KeyDownCount);
            Assert.AreEqual(0, owner.KeyUpCount);
            Assert.AreEqual(0, owner.ActiveCount);
            Assert.AreEqual(0, owner.FilterCount);
        }

        [TestMethod]
        public void FailedClosePropagatesAndRetainsOwnershipForRetry()
        {
            var owner = new CallbackOwner();
            using var hook = CreateHook(owner, out var native);
            native.Closing = () => throw new InvalidOperationException("Close failed");

            Assert.ThrowsExactly<InvalidOperationException>(hook.Dispose);
            native.Closing = null;
            Assert.IsFalse(hook.GetDisposedState());
            Assert.IsTrue(native.IsActive());

            hook.Dispose();

            Assert.IsTrue(hook.GetDisposedState());
            Assert.AreEqual(2, native.DisposeCount);
            AssertCallbacksInactive(native);
        }

        [TestMethod]
        public async Task ConcurrentDisposeClosesOnceAndKeepsCallbacksInactive()
        {
            var owner = new CallbackOwner();
            using var hook = CreateHook(owner, out var native);
            using var closing = new ManualResetEventSlim();
            using var finishClose = new ManualResetEventSlim();
            native.Closing = () =>
            {
                closing.Set();
                Assert.IsTrue(finishClose.Wait(TimeSpan.FromSeconds(10)));
            };

            var disposal = Task.Run(hook.Dispose);
            try
            {
                Assert.IsTrue(closing.Wait(TimeSpan.FromSeconds(10)));
                hook.Dispose();
                AssertCallbacksInactive(native);
            }
            finally
            {
                finishClose.Set();
                await disposal.WaitAsync(TimeSpan.FromSeconds(10));
            }

            Assert.IsTrue(hook.GetDisposedState());
            Assert.AreEqual(1, native.DisposeCount);
            Assert.AreEqual(0, owner.KeyDownCount);
            Assert.AreEqual(0, owner.KeyUpCount);
            Assert.AreEqual(0, owner.ActiveCount);
            Assert.AreEqual(0, owner.FilterCount);
        }

        [TestMethod]
        [DataRow("active")]
        [DataRow("filter")]
        [DataRow("keydown")]
        [DataRow("keyup")]
        public void CallbackCanDisposeItsOwnerWithoutReleasingInFlightNativeHook(string callback)
        {
            HotkeySettingsControlHook hook = null;
            WeakReference nativeReference = null;
            (KeyboardEventCallback KeyEvent, IsActiveCallback Active, FilterKeyboardEvent Filter) callbacks = default;
            void DisposeDuringCallback()
            {
                hook.Dispose();
                Collect();
                Assert.IsTrue(nativeReference.IsAlive, "Native hook must survive the in-flight callback.");
            }

            hook = new HotkeySettingsControlHook(
                _ => DisposeDuringCallback(),
                _ => DisposeDuringCallback(),
                () =>
                {
                    DisposeDuringCallback();
                    return true;
                },
                (_, _) =>
                {
                    DisposeDuringCallback();
                    return true;
                },
                (keyEvent, isActive, filter) =>
                {
                    callbacks = (keyEvent, isActive, filter);
                    var native = new TestHook(keyEvent, isActive, filter);
                    nativeReference = new WeakReference(native);
                    return native;
                });
            using (hook)
            {
                switch (callback)
                {
                    case "active":
                        Assert.IsFalse(callbacks.Active());
                        break;
                    case "filter":
                        Assert.IsFalse(callbacks.Filter(default));
                        break;
                    case "keydown":
                        callbacks.KeyEvent(new KeyboardEvent { message = 0x100 });
                        break;
                    case "keyup":
                        callbacks.KeyEvent(new KeyboardEvent { message = 0x101 });
                        break;
                }

                Collect();
                Assert.IsFalse(nativeReference.IsAlive, "Disposal must release the native back-reference after dispatch.");
                GC.KeepAlive(callbacks);
            }
        }

        [TestMethod]
        public void DisposeReleasesCallbackOwnerWhileHookAndNativeDelegatesRemainAlive()
        {
            var (hook, native, owner) = CreateRetainedCallbacks();
            using (hook)
            {
                Collect();
                Assert.IsTrue(owner.IsAlive);
                hook.Dispose();
                Collect();

                Assert.IsFalse(owner.IsAlive, "Disposed hooks must not retain any of their four callback targets.");
                AssertCallbacksInactive(native);
                GC.KeepAlive(native);
                GC.KeepAlive(hook);
            }
        }

        [TestMethod]
        public void DisposeBreaksRealUnstartedNativeCycleWhileManagedHookRemainsAlive()
        {
            var (hook, native, owner) = CreateNativeCycle();
            using (hook)
            {
                Collect();
                Assert.IsTrue(native.IsAlive);
                Assert.IsTrue(owner.IsAlive);

                hook.Dispose();
                Collect();

                Assert.IsFalse(native.IsAlive, "Close alone leaves the managed-to-native back-reference intact.");
                Assert.IsFalse(owner.IsAlive);
                GC.KeepAlive(hook);
            }
        }

        [TestMethod]
        public void DisposedRealNativeCycleDoesNotKeepManagedHookAlive()
        {
            var hook = CreateDisposedNativeCycle();
            Collect();
            Assert.IsFalse(hook.IsAlive, "Native WinRT delegates must no longer root the disposed managed hook.");
        }

        private static HotkeySettingsControlHook CreateHook(CallbackOwner owner, out TestHook native)
        {
            TestHook created = null;
            var hook = new HotkeySettingsControlHook(
                owner.KeyDown,
                owner.KeyUp,
                owner.IsActive,
                owner.FilterEvent,
                (keyEvent, isActive, filter) => created = new TestHook(keyEvent, isActive, filter));
            native = created;
            return hook;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (HotkeySettingsControlHook Hook, TestHook Native, WeakReference Owner) CreateRetainedCallbacks()
        {
            var owner = new CallbackOwner();
            var hook = CreateHook(owner, out var native);
            return (hook, native, new WeakReference(owner));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (HotkeySettingsControlHook Hook, WeakReference Native, WeakReference Owner) CreateNativeCycle()
        {
            var owner = new CallbackOwner();
            WeakReference native = null;
            var hook = new HotkeySettingsControlHook(
                owner.KeyDown,
                owner.KeyUp,
                owner.IsActive,
                owner.FilterEvent,
                (keyEvent, isActive, filter) =>
                {
                    // Exercise the real COM delegate cycle without registering a global keyboard hook.
                    var keyboardHook = new KeyboardHook(keyEvent, isActive, filter);
                    native = new WeakReference(keyboardHook);
                    return keyboardHook;
                });
            return (hook, native, new WeakReference(owner));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference CreateDisposedNativeCycle()
        {
            var (hook, _, _) = CreateNativeCycle();
            hook.Dispose();
            return new WeakReference(hook);
        }

        private static void AssertCallbacksInactive(TestHook native)
        {
            Assert.IsFalse(native.IsActive());
            Assert.IsFalse(native.Filter(default));
            native.KeyboardEvent(new KeyboardEvent { message = 0x100 });
            native.KeyboardEvent(new KeyboardEvent { message = 0x101 });
            native.KeyboardEvent(new KeyboardEvent { message = 0x104 });
            native.KeyboardEvent(new KeyboardEvent { message = 0x105 });
        }

        private static void Collect()
        {
            // The RCW finalizer releases COM delegates, whose managed targets need another collection.
            for (var i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private sealed class CallbackOwner
        {
            public bool Active { get; set; } = true;

            public bool Filter { get; set; } = true;

            public int KeyDownCount { get; private set; }

            public int KeyUpCount { get; private set; }

            public int ActiveCount { get; private set; }

            public int FilterCount { get; private set; }

            public int LastKey { get; private set; }

            public UIntPtr LastExtraInfo { get; private set; }

            public void KeyDown(int key)
            {
                KeyDownCount++;
                LastKey = key;
            }

            public void KeyUp(int key)
            {
                KeyUpCount++;
                LastKey = key;
            }

            public bool IsActive()
            {
                ActiveCount++;
                return Active;
            }

            public bool FilterEvent(int key, UIntPtr extraInfo)
            {
                FilterCount++;
                LastKey = key;
                LastExtraInfo = extraInfo;
                return Filter;
            }
        }

        private sealed class TestHook : IDisposable
        {
            public TestHook(KeyboardEventCallback keyboardEvent, IsActiveCallback isActive, FilterKeyboardEvent filter)
            {
                KeyboardEvent = keyboardEvent;
                IsActive = isActive;
                Filter = filter;
            }

            public KeyboardEventCallback KeyboardEvent { get; }

            public IsActiveCallback IsActive { get; }

            public FilterKeyboardEvent Filter { get; }

            public Action Closing { get; set; }

            public int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
                Closing?.Invoke();
            }
        }
    }
}
