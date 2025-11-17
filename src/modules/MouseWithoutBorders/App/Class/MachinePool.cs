// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <history>
//     Class created as refactoring by Tim Lovell-Smith (tilovell)
//     2023- Included in PowerToys.
// </history>
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.Class
{
    /// <summary>
    /// Holds our current 'last known state of the world' information about Machines the user wants to connect to.
    /// Keeps track of whether machines are 'alive' or 'disconnected' implicitly via timestamps.
    /// Only ever knows about Common.MAX_MACHINE machines.
    /// </summary>
    /// <remarks>
    /// Operations in the class are designed to be easily thread-safe.
    /// There are two factors helping this:
    ///
    /// 1) Minimal calls outside of itself.
    /// This class doesn't call other classes to do any 'interesting' or *potentially locking* work. This helps us avoid deadlock. (Common.GetTick() is safe)
    ///
    /// 2) Best-effort semantics. Callers will find that sometimes the thing they wanted to do just won't work,
    /// due to unfortunate timing - machine not found, too many machines, or whatever.
    /// e.g.
    /// - Every update is a 'try' to update.
    /// - Every find is a 'try' to find.
    /// etc.
    ///
    /// Please write regression tests (unit tests) for bugs before fixing them.
    /// </remarks>
    internal class MachinePool
    {
        private readonly Lock @lock;
        private readonly List<MachineInf> list;

        public MachinePool()
        {
            @lock = new Lock();
            list = new List<MachineInf>();
        }

        // This will set the timestamp to current time, making the machine 'alive'.
        internal bool TryUpdateMachineID(string machineName, ID id, bool updateTimeStamp)
        {
            bool rv = false;

            lock (@lock)
            {
                CheckName(machineName);
                for (int i = 0; i < list.Count; i++)
                {
                    if (NamesAreEqual(machineName, list[i].Name))
                    {
                        list[i] =

                            // This looks funny - but you have to rebuild the actual struct because List<T> doesn't let you make updates to struct fields.
                            new MachineInf
                            {
                                Name = list[i].Name,
                                Id = id,
                                Time = updateTimeStamp ? Common.GetTick() : list[i].Time,
                            };

                        rv = true;
                    }
                    else if (list[i].Id == id)
                    {
                        // Duplicate ID? Reset the old machine's.
                        list[i] = new MachineInf
                        {
                            Name = list[i].Name,
                            Id = ID.NONE,
                            Time = updateTimeStamp ? Common.GetTick() : list[i].Time,
                        };
                    }
                }
            }

            return rv;
        }

        /// <summary>
        /// Set a machine to 'disconnected' state due to Socket/Channel exception (overriding timestamp).
        /// Returns true if a matching machine name existed (regardless of whether the machine was already connected).
        /// </summary>
        /// <remarks>
        /// Explanation: When two machines are connected and one of them hibernates (or sleeps),
        /// the socket state of the other machine is still Connected and there would be an
        /// exception when we try to send the data using the socket.
        /// In this case we would want to remove the machine and move the Mouse control back to the host machine…
        /// </remarks>
        internal bool SetMachineDisconnected(string machineName)
        {
            lock (@lock)
            {
                bool foundAndTimedOut = false;
                CheckName(machineName);
                for (int i = 0; i < list.Count; i++)
                {
                    if (NamesAreEqual(machineName, list[i].Name))
                    {
                        list[i] =

                            // This looks funny - but you have to rebuild the actual struct because List<T> doesn't let you make updates to struct fields.
                            new MachineInf
                            {
                                Name = list[i].Name,
                                Id = list[i].Id,
                                Time = list[i].Time > Common.GetTick() - MachineStuff.HEARTBEAT_TIMEOUT + 10000 ? Common.GetTick() - MachineStuff.HEARTBEAT_TIMEOUT + 10000 : list[i].Time,
                            };

                        foundAndTimedOut = list[i].Time < Common.GetTick() - MachineStuff.HEARTBEAT_TIMEOUT + 10000 - 5000;
                    }
                }

                return foundAndTimedOut;
            }
        }

        // TODO: would probably be cleaner interface as IEnumerable
        internal List<MachineInf> ListAllMachines()
        {
            return Where((inf) => true);
        }

        internal List<MachineInf> TryFindMachineByID(ID id)
        {
            return id == ID.NONE ? new List<MachineInf>() : Where((inf) => inf.Id == id);
        }

        internal void Clear()
        {
            lock (@lock)
            {
                list.Clear();
            }
        }

        public void Initialize(IEnumerable<string> machineNames)
        {
            lock (@lock)
            {
                list.Clear();

                foreach (string name in machineNames)
                {
                    if (string.IsNullOrEmpty(name.Trim()))
                    {
                        continue; // next
                    }
                    else if (list.Count >= 4)
                    {
                        throw new ArgumentException($"The number of machines exceeded the maximum allowed limit of {MachineStuff.MAX_MACHINE}. Actual count: {list.Count}.");
                    }

                    _ = LearnMachine(name);
                }
            }
        }

        public void Initialize(IEnumerable<MachineInf> infos)
        {
            lock (@lock)
            {
                list.Clear();

                foreach (MachineInf inf in infos)
                {
                    if (string.IsNullOrEmpty(inf.Name.Trim()))
                    {
                        continue; // next
                    }
                    else if (list.Count >= 4)
                    {
                        throw new ArgumentException($"The number of machines exceeded the maximum allowed limit of {MachineStuff.MAX_MACHINE}. Actual count: {list.Count}.");
                    }

                    _ = LearnMachine(inf.Name);
                    _ = TryUpdateMachineID(inf.Name, inf.Id, false);
                }
            }
        }

        // Add a new machine to our set of known machines. Initially it is 'disconnected'.
        // Fails and return false if the machine pool is already full.
        public bool LearnMachine(string machineName)
        {
            if (machineName == null)
            {
                throw new ArgumentNullException(machineName);
            }
            else if (string.IsNullOrEmpty(machineName.Trim()))
            {
                throw new ArgumentException(machineName);
            }

            lock (@lock)
            {
                CheckName(machineName);
                for (int i = 0; i < list.Count; i++)
                {
                    if (NamesAreEqual(list[i].Name, machineName))
                    {
                        return false; // already in list
                    }
                }

                if (list.Count >= MachineStuff.MAX_MACHINE)
                {
                    int slotFound = -1;

                    for (int i = 0; i < list.Count; i++)
                    {
                        if (!MachineStuff.InMachineMatrix(list[i].Name))
                        {
                            slotFound = i;
                            break;
                        }
                    }

                    if (slotFound >= 0)
                    {
                        list.RemoveAt(slotFound);
                    }
                    else
                    {
                        return false;
                    }
                }

                list.Add(new MachineInf { Name = machineName });
                return true;
            }
        }

        internal bool TryFindMachineByName(string machineName, out MachineInf result)
        {
            lock (@lock)
            {
                CheckName(machineName);
                for (int i = 0; i < list.Count; i++)
                {
                    if (NamesAreEqual(list[i].Name, machineName))
                    {
                        result = list[i];
                        return true;
                    }
                }

                result = default;
                return false;
            }
        }

        internal ID ResolveID(string machineName)
        {
            return TryFindMachineByName(machineName, out MachineInf inf) ? inf.Id : ID.NONE;
        }

        private List<MachineInf> Where(Predicate<MachineInf> test)
        {
            lock (@lock)
            {
                List<MachineInf> ret = new();

                for (int i = 0; i < list.Count; i++)
                {
                    if (test(list[i]))
                    {
                        ret.Add(list[i]);
                    }
                }

                return ret;
            }
        }

        internal string SerializedAsString()
        {
            lock (@lock)
            {
                List<MachineInf> machinePool = ListAllMachines();
                string rv = string.Join(",", machinePool.Select(m => $"{m.Name}:{m.Id}"));

                for (int j = machinePool.Count; j < MachineStuff.MAX_MACHINE; j++)
                {
                    rv += ",:";
                }

                return rv;
            }
        }

        /// <param name="clockSkewInMS_forTesting">When doing unit tests it's nice to be able to fudge with the clock time, adding milliseconds, instead of sleeping.</param>
        internal static bool IsAlive(MachineInf inf, int clockSkewInMS_forTesting = 0)
        {
            return inf.Id != ID.NONE && (Common.GetTick() + clockSkewInMS_forTesting - inf.Time <= MachineStuff.HEARTBEAT_TIMEOUT || Common.IsConnectedTo(inf.Id));
        }

        private static bool NamesAreEqual(string name1, string name2)
        {
            return string.Equals(name1, name2, StringComparison.OrdinalIgnoreCase);
        }

        private static void CheckName(string machineName)
        {
            Debug.Assert(machineName != null, "machineName is null");
            Debug.Assert(machineName.Trim().Length == machineName.Length, "machineName contains spaces");
        }

        internal void ResetIPAddressesForDeadMachines(bool firstLoaded = false)
        {
            lock (@lock)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (!string.IsNullOrEmpty(list[i].Name) && list[i].Name.Equals(Common.MachineName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if ((firstLoaded && !MachineStuff.InMachineMatrix(list[i].Name)) || (!firstLoaded && (!MachineStuff.InMachineMatrix(list[i].Name) || !IsAlive(list[i]))))
                    {
                        list[i] =
                            new MachineInf
                            {
                                Name = list[i].Name,
                                Id = ID.NONE,
                                Time = list[i].Time,
                            };
                    }
                }
            }
        }
    }
}
