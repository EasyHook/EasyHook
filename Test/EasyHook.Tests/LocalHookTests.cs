using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EasyHook.Tests
{
    [TestClass]
    public class LocalHookTests
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool Beep(uint dwFreq, uint dwDuration);

        [return: MarshalAs(UnmanagedType.Bool)]
        delegate bool BeepDelegate(uint dwFreq, uint dwDuration);

        [return: MarshalAs(UnmanagedType.Bool)]
        bool BeepHook(uint dwFreq, uint dwDuration)
        {
            return Beep(dwFreq, dwDuration);
        }

        [TestInitialize]
        public void Initialise()
        {
            NativeAPI.LhWaitForPendingRemovals();
        }

        [TestMethod]
        public void InstallTooManyHooks_ThrowException()
        {
            int maxHookCount = 1024;

            List<LocalHook> hooks = new List<LocalHook>();

            // Install MAX_HOOK_COUNT hooks (i.e. 1024)
            for (var i = 0; i < maxHookCount; i++)
            {
                LocalHook lh = LocalHook.Create(
                    LocalHook.GetProcAddress("kernel32.dll", "Beep"),
                    new BeepDelegate(BeepHook),
                    this);
                hooks.Add(lh);
            }

            // NOTE: Disposing hooks does not free the memory
            // need to also call NativeAPI.LhWaitForPendingRemovals()
            // or LocalHook.Release();
            foreach (var h in hooks)
                h.Dispose();
            hooks.Clear();

            bool exceptionThrown = false;
            try
            {
                // Adding one more hook should result in System.InsufficientMemoryException
                hooks.Add(LocalHook.Create(
                        LocalHook.GetProcAddress("kernel32.dll", "Beep"),
                        new BeepDelegate(BeepHook),
                        this));

                foreach (var h in hooks)
                    h.Dispose();
                hooks.Clear();
            }
            catch (System.InsufficientMemoryException)
            {
                // Correctly threw error because too many hooks
                exceptionThrown = true;
            }

            Assert.IsTrue(exceptionThrown, "System.InsufficientMemoryException was not thrown");

            // Ensure the hooks are freed
            NativeAPI.LhWaitForPendingRemovals();

            // Now try to install again after removals processed
            try
            {
                hooks.Add(LocalHook.Create(
                    LocalHook.GetProcAddress("kernel32.dll", "Beep"),
                    new BeepDelegate(BeepHook),
                    this));
            }
            catch (System.InsufficientMemoryException)
            {
                Assert.Fail("Disposing of hooks did not free room within GlobalSlotList");
            }
            foreach (var h in hooks)
                h.Dispose();
            hooks.Clear();

            // Ensure the hooks are freed
            NativeAPI.LhWaitForPendingRemovals();
        }
    }
}
