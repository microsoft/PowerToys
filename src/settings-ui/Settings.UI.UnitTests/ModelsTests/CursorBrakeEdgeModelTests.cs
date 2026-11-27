// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class CursorBrakeEdgeModelTests
    {
        private const string Hp = @"\\?\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352";
        private const string Dell = @"\\?\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353";
        private const string Lenovo = @"\\?\DISPLAY#LEN66F1#5&1a2b3c4d&0&UID4354";
        private const string Lg = @"\\?\DISPLAY#LGD0A1B#5&1a2b3c4d&0&UID4355";

        private const CursorBrakeEdgeMode Custom = CursorBrakeEdgeMode.Custom;
        private const CursorBrakeEdgeMode Off = CursorBrakeEdgeMode.Off;
        private const CursorBrakeEdgeMode Wall = CursorBrakeEdgeMode.Wall;

        private static CursorBrakeEdgeSetting Both(string first, string second, CursorBrakeEdgeMode mode, int pushDistance = 300)
        {
            return new CursorBrakeEdgeSetting { Monitors = { first, second }, Mode = mode, PushDistance = pushDistance };
        }

        private static CursorBrakeEdgeSetting One(string first, string second, CursorBrakeEdgeMode mode, int pushDistance = 300)
        {
            return new CursorBrakeEdgeSetting { Monitors = { first, second }, OneWay = true, Mode = mode, PushDistance = pushDistance };
        }

        private static string Describe(IEnumerable<CursorBrakeEdgeSetting> edges)
        {
            return string.Join("; ", edges.Select(e => $"{string.Join(e.OneWay ? " -> " : " <-> ", e.Monitors)} mode {e.Mode} {e.PushDistance}"));
        }

        private static void AssertEdges(IEnumerable<CursorBrakeEdgeSetting> expected, IEnumerable<CursorBrakeEdgeSetting> actual, string because)
        {
            Assert.AreEqual(Describe(expected), Describe(actual), because);
        }

        private static CursorBrakeEdgeValue Value(CursorBrakeEdgeMode mode, int pushDistance = 300)
        {
            return new CursorBrakeEdgeValue(mode, pushDistance);
        }

        // HP (main) | DELL, LENOVO above both.
        private static CursorBrakeDetectedEdges Detected(params (int First, int Second)[] edges)
        {
            var detected = new CursorBrakeDetectedEdges { Version = 1, Identified = true };
            detected.Monitors.Add(new CursorBrakeDetectedMonitor { Id = Hp, Name = "HP E24 G5", Rect = { 0, 0, 1920, 1080 }, Dpi = 96, Primary = true });
            detected.Monitors.Add(new CursorBrakeDetectedMonitor { Id = Dell, Name = "Dell U2724D", Rect = { 1920, 0, 4480, 1440 }, Dpi = 144 });
            detected.Monitors.Add(new CursorBrakeDetectedMonitor { Id = Lenovo, Name = "Lenovo L32p-30", Rect = { 0, -2160, 3840, 0 }, Dpi = 192 });
            foreach (var (first, second) in edges)
            {
                detected.Edges.Add(new CursorBrakeDetectedEdge { Monitors = { first, second }, Axis = "vertical" });
            }

            return detected;
        }

        [TestMethod]
        public void EdgeSettingsRoundTripInTheDesignedShape()
        {
            var properties = new CursorBrakeProperties();
            properties.Edges.Value.Add(Both(Dell, Hp, Custom, 600));
            properties.Edges.Value.Add(One(Lenovo, Dell, Wall));

            var json = JsonSerializer.Serialize(properties);
            using var document = JsonDocument.Parse(json);
            var entries = document.RootElement.GetProperty("edges").GetProperty("value");

            Assert.AreEqual(2, entries.GetArrayLength());
            Assert.IsFalse(entries[0].TryGetProperty("one_way", out _), "A both-ways entry leaves one_way out.");
            Assert.IsTrue(entries[1].GetProperty("one_way").GetBoolean());
            Assert.AreEqual(Dell, entries[0].GetProperty("monitors")[0].GetString());
            Assert.AreEqual(600, entries[0].GetProperty("push_distance").GetInt32());
            Assert.AreEqual(2, entries[1].GetProperty("mode").GetInt32(), "A wall is mode 2 in the file, as the module reads it.");

            var read = JsonSerializer.Deserialize<CursorBrakeProperties>(json);
            AssertEdges(properties.Edges.Value, read.Edges.Value, "The entries read back as written.");
        }

        [TestMethod]
        public void ASettingsFileWithoutEdgesReadsAsNoEntries()
        {
            var read = JsonSerializer.Deserialize<CursorBrakeProperties>("""{ "push_distance": { "value": 300 } }""");

            Assert.IsNotNull(read.Edges);
            Assert.IsEmpty(read.Edges.Value);
            Assert.IsEmpty(new CursorBrakeProperties().Edges.Value);
        }

        [TestMethod]
        public void LookupChecksOneWayThenBothWaysThenDefault()
        {
            var cases = new (string Name, List<CursorBrakeEdgeSetting> Edges, string From, string To, CursorBrakeEdgeValue Expected)[]
            {
                ("no entries", [], Hp, Dell, Value(CursorBrakeEdgeMode.Default)),
                ("both ways, in order", [Both(Hp, Dell, Custom, 600)], Hp, Dell, Value(CursorBrakeEdgeMode.Custom, 600)),
                ("both ways, reversed", [Both(Dell, Hp, Custom, 600)], Hp, Dell, Value(CursorBrakeEdgeMode.Custom, 600)),
                ("one way, its direction", [One(Hp, Dell, Wall)], Hp, Dell, Value(CursorBrakeEdgeMode.Wall)),
                ("one way, the other direction", [One(Dell, Hp, Wall)], Hp, Dell, Value(CursorBrakeEdgeMode.Default)),
                ("one way listed after both ways", [Both(Hp, Dell, Custom, 600), One(Hp, Dell, Off, 450)], Hp, Dell, Value(CursorBrakeEdgeMode.Off, 450)),
                ("two both-ways entries", [Both(Hp, Dell, Custom, 600), Both(Dell, Hp, Wall)], Hp, Dell, Value(CursorBrakeEdgeMode.Custom, 600)),
                ("IDs in another case", [Both(Hp.ToLowerInvariant(), Dell.ToUpperInvariant(), Off)], Hp, Dell, Value(CursorBrakeEdgeMode.Off)),
                ("an unknown mode is skipped", [Both(Hp, Dell, (CursorBrakeEdgeMode)7), Both(Hp, Dell, Wall)], Hp, Dell, Value(CursorBrakeEdgeMode.Wall)),
                ("one ID is skipped", [new CursorBrakeEdgeSetting { Monitors = { Hp }, Mode = Wall }], Hp, Dell, Value(CursorBrakeEdgeMode.Default)),
                ("an entry for another pair", [Both(Hp, Lenovo, Wall)], Hp, Dell, Value(CursorBrakeEdgeMode.Default)),
            };

            foreach (var c in cases)
            {
                Assert.AreEqual(c.Expected, CursorBrakeEdgeModel.Lookup(c.Edges, c.From, c.To), c.Name);
            }
        }

        [TestMethod]
        public void EqualDirectionsWriteOneBothWaysEntryInSortedOrder()
        {
            var edges = new List<CursorBrakeEdgeSetting> { One(Hp, Dell, Wall), One(Dell, Hp, Wall) };

            var written = CursorBrakeEdgeModel.SetPair(edges, Hp, Dell, Value(CursorBrakeEdgeMode.Custom, 600), Value(CursorBrakeEdgeMode.Custom, 600));

            // OrdinalIgnoreCase puts DELA1B2 before HWP3A6E.
            AssertEdges([Both(Dell, Hp, Custom, 600)], written, "Both directions alike collapse.");
        }

        [TestMethod]
        public void DifferentDirectionsWriteOneEntryPerDirectionThatIsNotDefault()
        {
            var cases = new (string Name, CursorBrakeEdgeValue HpToDell, CursorBrakeEdgeValue DellToHp, List<CursorBrakeEdgeSetting> Expected)[]
            {
                ("Wall one way, Default back", Value(CursorBrakeEdgeMode.Wall), Value(CursorBrakeEdgeMode.Default), [One(Hp, Dell, Wall)]),
                ("Default one way, Wall back", Value(CursorBrakeEdgeMode.Default), Value(CursorBrakeEdgeMode.Wall), [One(Dell, Hp, Wall)]),
                ("Custom one way, Off back", Value(CursorBrakeEdgeMode.Custom, 600), Value(CursorBrakeEdgeMode.Off, 250), [One(Hp, Dell, Custom, 600), One(Dell, Hp, Off, 250)]),
                ("both Default", Value(CursorBrakeEdgeMode.Default), Value(CursorBrakeEdgeMode.Default), []),
                ("both Off, keeping the distance", Value(CursorBrakeEdgeMode.Off, 450), Value(CursorBrakeEdgeMode.Off, 450), [Both(Dell, Hp, Off, 450)]),
            };

            foreach (var c in cases)
            {
                var edges = new List<CursorBrakeEdgeSetting> { Both(Hp, Dell, Custom, 900), One(Dell, Hp, Wall), Both(Dell, Hp, (CursorBrakeEdgeMode)7) };
                AssertEdges(c.Expected, CursorBrakeEdgeModel.SetPair(edges, Hp, Dell, c.HpToDell, c.DellToHp), c.Name);
            }
        }

        [TestMethod]
        public void SettingAPairLeavesOtherPairsInPlace()
        {
            var edges = new List<CursorBrakeEdgeSetting> { Both(Hp, Lenovo, Off), One(Dell, Hp, Wall), Both(Dell, Lenovo, (CursorBrakeEdgeMode)7), Both(Lg, Hp, Wall) };

            var written = CursorBrakeEdgeModel.SetPair(edges, Hp, Dell, Value(CursorBrakeEdgeMode.Custom, 600), Value(CursorBrakeEdgeMode.Custom, 600));

            AssertEdges([Both(Hp, Lenovo, Off), Both(Dell, Hp, Custom, 600), Both(Dell, Lenovo, (CursorBrakeEdgeMode)7), Both(Lg, Hp, Wall)], written, "The pair takes the place of its first entry, and the rest stay as they are.");
            AssertEdges([Both(Hp, Lenovo, Off), One(Dell, Hp, Wall), Both(Dell, Lenovo, (CursorBrakeEdgeMode)7), Both(Lg, Hp, Wall)], edges, "The input list is not changed.");
        }

        [TestMethod]
        public void NormalizingRewritesEachPairAsTheEngineReadsIt()
        {
            // Overlapping entries, duplicates and one-way pairs with equal values all reduce to what
            // the lookup decides. Entries with an unknown mode or without two IDs stay untouched.
            var edges = new List<CursorBrakeEdgeSetting>
            {
                Both(Hp, Dell, Custom, 600),
                One(Dell, Hp, Wall),
                Both(Dell, Hp, Off),
                One(Lenovo, Hp, Wall),
                One(Hp, Lenovo, Wall),
                Both(Lg, Hp, (CursorBrakeEdgeMode)7),
                new CursorBrakeEdgeSetting { Monitors = { Hp }, Mode = Wall },
            };

            var normalized = CursorBrakeEdgeModel.Normalize(edges);

            AssertEdges(
                [
                    One(Hp, Dell, Custom, 600),
                    One(Dell, Hp, Wall),
                    Both(Hp, Lenovo, Wall),
                    Both(Lg, Hp, (CursorBrakeEdgeMode)7),
                    new CursorBrakeEdgeSetting { Monitors = { Hp }, Mode = Wall },
                ],
                normalized,
                "Each pair is rewritten in place of its first entry.");
            AssertEdges(normalized, CursorBrakeEdgeModel.Normalize(normalized), "Normalizing twice changes nothing more.");
        }

        [TestMethod]
        public void PruningDropsEveryEntryForAPairNotSeen()
        {
            var edges = new List<CursorBrakeEdgeSetting> { Both(Hp, Dell, Wall), One(Lg, Hp, Wall), One(Hp, Lg, Off), Both(Hp, Lenovo, (CursorBrakeEdgeMode)7), new CursorBrakeEdgeSetting { Monitors = { Lg }, Mode = Wall } };
            var seen = new List<CursorBrakeSeenPair>
            {
                new CursorBrakeSeenPair { Monitors = { Dell.ToLowerInvariant(), Hp }, LastSeenUtc = "2026-10-01T08:12:00Z" },
                new CursorBrakeSeenPair { Monitors = { Hp, Lenovo }, LastSeenUtc = "2026-10-01T08:12:00Z" },
            };

            var pruned = CursorBrakeEdgeModel.Prune(edges, seen, out bool changed);

            Assert.IsTrue(changed);
            AssertEdges([Both(Hp, Dell, Wall), Both(Hp, Lenovo, (CursorBrakeEdgeMode)7), new CursorBrakeEdgeSetting { Monitors = { Lg }, Mode = Wall }], pruned, "Both LG entries go; seen pairs match in either order and case, and an entry without two IDs stays.");
        }

        [TestMethod]
        public void PruningWithEverythingSeenChangesNothing()
        {
            var edges = new List<CursorBrakeEdgeSetting> { Both(Hp, Dell, Wall) };
            var seen = new List<CursorBrakeSeenPair> { new CursorBrakeSeenPair { Monitors = { Hp, Dell }, LastSeenUtc = "2026-10-01T08:12:00Z" } };

            var pruned = CursorBrakeEdgeModel.Prune(edges, seen, out bool changed);

            Assert.IsFalse(changed);
            AssertEdges(edges, pruned, "Nothing to prune.");
        }

        [TestMethod]
        public void TheTrapCheckFlagsOnlyADisplayThatCanBeEnteredButNotLeft()
        {
            // Edges: HP-DELL (0-1), HP-LENOVO (0-2), DELL-LENOVO (1-2).
            var detected = Detected((0, 1), (0, 2), (1, 2));
            var cases = new (string Name, List<CursorBrakeEdgeSetting> Edges, int[] Expected)[]
            {
                ("no entries", [], []),
                ("every way out of LENOVO walled, the ways in open", [One(Lenovo, Hp, Wall), One(Lenovo, Dell, Wall)], [2]),
                ("every way out and in walled", [Both(Lenovo, Hp, Wall), Both(Lenovo, Dell, Wall)], []),
                ("one way out of LENOVO open", [One(Lenovo, Hp, Wall)], []),
                ("one way in off, the other walled", [Both(Lenovo, Hp, Wall), One(Lenovo, Dell, Wall), One(Dell, Lenovo, Off)], [2]),
                ("two trapped displays", [One(Lenovo, Hp, Wall), One(Lenovo, Dell, Wall), One(Dell, Hp, Wall), One(Dell, Lenovo, Wall), One(Hp, Lenovo, Custom, 600)], [1, 2]),
            };

            foreach (var c in cases)
            {
                CollectionAssert.AreEqual(c.Expected, CursorBrakeEdgeModel.TrappedMonitors(detected, c.Edges).ToArray(), c.Name);
            }
        }

        [TestMethod]
        public void ADisplayWithoutAnEdgeOrIdIsNeverTrapped()
        {
            var detected = Detected((0, 1));
            detected.Monitors[1].Id = string.Empty;

            CollectionAssert.AreEqual(System.Array.Empty<int>(), CursorBrakeEdgeModel.TrappedMonitors(detected, [One(Lenovo, Hp, Wall), One(Dell, Hp, Wall)]).ToArray(), "LENOVO has no edge, and DELL has no ID, so no entry reaches it.");
        }

        // A (0, HP), B (1, DELL) on A's right, C (2, LENOVO) meeting A only at A's bottom-right
        // corner, and D (3, LG) below A, with the edges the module publishes for them. `shortB` ends
        // B at y = 800, so nothing borders A's right side at the corner.
        private static CursorBrakeDetectedEdges Corner(bool shortB, bool withD)
        {
            var detected = new CursorBrakeDetectedEdges { Version = 1, Identified = true };
            detected.Monitors.Add(new CursorBrakeDetectedMonitor { Id = Hp, Rect = { 0, 0, 1920, 1080 }, Dpi = 96 });
            detected.Monitors.Add(new CursorBrakeDetectedMonitor { Id = Dell, Rect = { 1920, 0, 3840, shortB ? 800 : 1080 }, Dpi = 96 });
            detected.Monitors.Add(new CursorBrakeDetectedMonitor { Id = Lenovo, Rect = { 1920, 1080, 3840, 2160 }, Dpi = 96 });
            var edges = new List<(int, int)> { (0, 1) };
            if (!shortB)
            {
                edges.Add((1, 2));
            }

            if (withD)
            {
                detected.Monitors.Add(new CursorBrakeDetectedMonitor { Id = Lg, Rect = { 0, 1080, 1920, 2160 }, Dpi = 96 });
                edges.Add((0, 3));
                edges.Add((2, 3));
            }

            foreach (var (first, second) in edges)
            {
                detected.Edges.Add(new CursorBrakeDetectedEdge { Monitors = { first, second }, Axis = "vertical" });
            }

            return detected;
        }

        [TestMethod]
        public void TheTrapCheckCountsTheWaysThroughACorner()
        {
            // The module takes the stricter side of a corner, so a corner is a way out unless a
            // wall borders it on one side. Mirrors the module's TrappedDisplaysCountTheWaysThroughACorner.
            var cases = new (string Name, CursorBrakeDetectedEdges Detected, List<CursorBrakeEdgeSetting> Edges, int[] Expected, int[] WaysIntoA)[]
            {
                ("the wall to B also closes the corner", Corner(false, false), [One(Hp, Dell, Wall)], [0], [1, 2]),
                ("B ends short of the corner, which stays open", Corner(true, false), [One(Hp, Dell, Wall)], [], [1, 2]),
                ("walled both ways on every shared edge, entered through the corner", Corner(true, true), [Both(Hp, Dell, Wall), Both(Hp, Lg, Wall)], [0], [2]),
                ("the way in through the corner walled too, which traps C instead", Corner(true, true), [Both(Hp, Dell, Wall), Both(Hp, Lg, Wall), One(Lenovo, Lg, Wall)], [2], []),
            };

            foreach (var c in cases)
            {
                CollectionAssert.AreEqual(c.Expected, CursorBrakeEdgeModel.TrappedMonitors(c.Detected, c.Edges).ToArray(), c.Name);
                CollectionAssert.AreEqual(c.WaysIntoA, CursorBrakeEdgeModel.WaysIn(c.Detected, c.Edges, 0).ToArray(), c.Name + ", the ways into A");
            }
        }

        // Malformed settings.json shapes, as DSC or a hand edit writes them. The
        // module skips each bad entry alone, and Settings drops it at load.
        [TestMethod]
        [DataRow("null", DisplayName = "a null entry")]
        [DataRow("""{ "monitors": null, "mode": 2 }""", DisplayName = "null monitors")]
        [DataRow("""{ "monitors": [ null, "b" ], "mode": 2 }""", DisplayName = "a null ID")]
        [DataRow("""{ "monitors": [ "a" ], "mode": 2 }""", DisplayName = "one ID")]
        [DataRow("""{ "monitors": [ "a", "b", "c" ], "mode": 2 }""", DisplayName = "three IDs")]
        [DataRow("""{ "monitors": [ "a", "b" ], "mode": "2" }""", DisplayName = "a mode that is a string")]
        [DataRow("""{ "monitors": [ "a", "b" ], "mode": 2, "one_way": "true" }""", DisplayName = "one_way a string")]
        [DataRow("""{ "monitors": [ "a", "b" ], "mode": 0, "push_distance": "600" }""", DisplayName = "push_distance a string")]
        [DataRow("""{ "monitors": [ "a", 7 ], "mode": 2 }""", DisplayName = "an ID that is a number")]
        [DataRow("42", DisplayName = "not an object")]
        public void AnEntryTheModuleSkipsIsDroppedAtLoad(string entry)
        {
            string json = $$"""{ "edges": { "value": [ { "monitors": [ "{{Hp.Replace(@"\", @"\\")}}", "{{Dell.Replace(@"\", @"\\")}}" ], "mode": 2 }, {{entry}} ] } }""";

            var read = JsonSerializer.Deserialize<CursorBrakeProperties>(json);

            AssertEdges([Both(Hp, Dell, Wall)], CursorBrakeEdgeModel.Sanitize(read.Edges.Value), "Only the good entry stays.");
        }

        [TestMethod]
        public void AWrongTypedEntryKeepsTheRestOfTheFile()
        {
            const string json = """
                { "name": "CursorBrake", "version": "1.0",
                  "properties": { "push_distance": { "value": 450 }, "free_return": { "value": false },
                    "edges": { "value": [ { "monitors": [ "a", "b" ], "mode": "2" } ] } } }
                """;

            var read = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.CursorBrakeSettings);

            Assert.AreEqual(450, read.Properties.PushDistance.Value, "The global distance survives a bad entry.");
            Assert.IsFalse(read.Properties.FreeReturn.Value);
            Assert.IsEmpty(CursorBrakeEdgeModel.Sanitize(read.Properties.Edges.Value));
        }

        private static CursorBrakeSettings ReadSettingsWith(string entry)
        {
            string json = """{ "name": "CursorBrake", "version": "1.0", "properties": { "push_distance": { "value": 450 }, "edges": { "value": [ { "monitors": [ "c", "d" ], "mode": 1 }, """ + entry + " ] } } }";
            return JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.CursorBrakeSettings);
        }

        // Shapes the module's parser applies, so Settings shows them as in force. Its parser keeps
        // the last copy of a field given twice, and it reads past any unknown field.
        [TestMethod]
        [DataRow("""{ "monitors": [ "a", "b" ], "mode": "x", "mode": 2 }""", false, DisplayName = "a wrong-typed mode, then a good one")]
        [DataRow("""{ "monitors": 5, "monitors": [ "a", "b" ], "mode": 2 }""", false, DisplayName = "wrong-typed monitors, then good ones")]
        [DataRow("""{ "monitors": [ "a", "b" ], "one_way": "x", "one_way": true, "mode": 2 }""", true, DisplayName = "a wrong-typed one_way, then a good one")]
        [DataRow("""{ "monitors": [ "a", "b" ], "mode": 2, "\ud800": 1 }""", false, DisplayName = "an unknown field named with a lone surrogate")]
        [DataRow("""{ "monitors": [ "a", "b" ], "mode": 2, "note": "\ud800" }""", false, DisplayName = "an unknown field holding a lone surrogate")]
        [DataRow("""{ "monitors": [ "a", "b" ], "mode": 2, "x": [[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]] }""", false, DisplayName = "an unknown field nested 50 deep")]
        public void AnEntryTheModuleAppliesIsKeptAtLoad(string entry, bool oneWay)
        {
            var read = ReadSettingsWith(entry);

            Assert.AreEqual(450, read.Properties.PushDistance.Value, "The rest of the file stays.");
            AssertEdges([Both("c", "d", Off), oneWay ? One("a", "b", Wall) : Both("a", "b", Wall)], CursorBrakeEdgeModel.Sanitize(read.Properties.Edges.Value), "Both entries stay.");
        }

        // Shapes that drop their entry and keep the rest of the file. The module skips a field
        // whose last copy has the wrong type. It reads an ID holding a lone surrogate, but no
        // monitor carries one, so that entry never applies. A number past double's range makes the
        // module's parser reject the whole file, keeping what it had, until Settings next saves
        // the file without that number.
        [TestMethod]
        [DataRow("""{ "monitors": [ "a", "b" ], "mode": 2, "mode": "x" }""", DisplayName = "a good mode, then a wrong-typed one")]
        [DataRow("""{ "monitors": [ "\ud800", "b" ], "mode": 2 }""", DisplayName = "a lone surrogate in an ID")]
        [DataRow("""{ "monitors": [ "a", "b" ], "mode": 1e400 }""", DisplayName = "a mode past double's range")]
        [DataRow("""{ "monitors": [ "a", "b" ], "mode": 0, "push_distance": 1e400 }""", DisplayName = "a distance past double's range")]
        public void AnEntrySettingsCannotShowIsDroppedAlone(string entry)
        {
            var read = ReadSettingsWith(entry);

            Assert.AreEqual(450, read.Properties.PushDistance.Value, "The rest of the file stays.");
            AssertEdges([Both("c", "d", Off)], CursorBrakeEdgeModel.Sanitize(read.Properties.Edges.Value), "Only the good entry stays.");
        }

        [TestMethod]
        public void AFieldNestedPastTheSerializersDepthFailsTheWholeFile()
        {
            // The serializer's reader stops at depth 64, before the edges converter can skip the
            // field, so the upstream limit stands: the file fails to load as a whole.
            string nested = new string('[', 70) + new string(']', 70);

            Assert.Throws<JsonException>(() => ReadSettingsWith("""{ "monitors": [ "a", "b" ], "mode": 2, "x": """ + nested + " }"));
        }

        [TestMethod]
        public void AnEdgesValueThatIsNotAListReadsAsNoEntries()
        {
            var read = JsonSerializer.Deserialize<CursorBrakeProperties>("""{ "push_distance": { "value": 450 }, "edges": { "value": 5 } }""");

            Assert.AreEqual(450, read.PushDistance.Value);
            Assert.IsEmpty(CursorBrakeEdgeModel.Sanitize(read.Edges.Value));
        }

        [TestMethod]
        public void LoadingAppliesTheModulesLimits()
        {
            string longId = @"\\?\DISPLAY#" + new string('A', 501);
            var edges = new List<CursorBrakeEdgeSetting>
            {
                Both(Hp, Dell, Custom, -50),
                Both(Hp, Lenovo, Custom, 5000),
                Both(Hp, longId, Wall),
                Both(Hp, Lg, Off, 450),
            };

            AssertEdges([Both(Hp, Dell, Custom, 0), Both(Hp, Lenovo, Custom, 1000), Both(Hp, Lg, Off, 450)], CursorBrakeEdgeModel.Sanitize(edges), "Distances clamp into 0 to 1000, and an ID past 512 characters drops its entry.");
        }

        [TestMethod]
        public void OnlyTheFirst256EntriesAreRead()
        {
            // The module reads the first 256 elements, bad ones included, so Settings does too.
            var edges = new List<CursorBrakeEdgeSetting> { null };
            for (int i = 1; i < 300; i++)
            {
                edges.Add(Both($@"\\?\DISPLAY#A#{i}", Hp, Off));
            }

            var read = CursorBrakeEdgeModel.Sanitize(edges);

            Assert.HasCount(255, read);
            Assert.AreEqual(@"\\?\DISPLAY#A#255", read[^1].Monitors[0]);
        }

        [TestMethod]
        public void IdsFoldOnlyAsciiCase()
        {
            var ascii = new List<CursorBrakeEdgeSetting> { Both(@"\\?\DISPLAY#ABC1#1", Hp, Wall) };
            var accented = new List<CursorBrakeEdgeSetting> { Both("\\\\?\\DISPLAY#\u00C9CR1#1", Hp, Wall) };

            Assert.AreEqual(Value(CursorBrakeEdgeMode.Wall), CursorBrakeEdgeModel.Lookup(ascii, @"\\?\display#abc1#1", Hp), "ASCII letters fold, as the module folds them.");
            Assert.AreEqual(Value(CursorBrakeEdgeMode.Default), CursorBrakeEdgeModel.Lookup(accented, "\\\\?\\DISPLAY#\u00E9CR1#1", Hp), "Other letters keep their case, as in the module.");
        }

        // Malformed detected-edges.json shapes. The module rewrites the file
        // whole on its next publish, so Settings only has to read it without failing.
        [TestMethod]
        [DataRow("""{ "version": 1, "monitors": null, "edges": [], "seen": [] }""", 0, DisplayName = "monitors null")]
        [DataRow("""{ "version": 1, "monitors": [ null, null ], "edges": [], "seen": [] }""", 2, DisplayName = "null monitors")]
        [DataRow("""{ "version": 1, "monitors": [ { "id": null, "name": null, "rect": null } ], "edges": [], "seen": [] }""", 1, DisplayName = "a monitor's fields null")]
        [DataRow("""{ "version": 1, "monitors": [], "edges": null, "seen": null }""", 0, DisplayName = "edges and seen null")]
        [DataRow("""{ "version": 1, "monitors": [ { "id": "a", "rect": [0, 0, 1, 1] }, { "id": "b", "rect": [1, 0, 2, 1] } ], "edges": [ { "monitors": null }, null ], "seen": [] }""", 2, DisplayName = "an edge's monitors null")]
        [DataRow("""{ "version": 1, "monitors": [], "edges": [], "seen": [ null, { "monitors": null, "last_seen_utc": null } ] }""", 0, DisplayName = "seen entries null")]
        public void ADetectedFileWithNullsReadsAsEmpty(string json, int monitors)
        {
            var detected = CursorBrakeEdgeModel.Clean(JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.CursorBrakeDetectedEdges));

            Assert.HasCount(monitors, detected.Monitors);
            Assert.HasCount(monitors, CursorBrakeEdgeModel.Numbers(detected).ToList());
            Assert.IsEmpty(CursorBrakeEdgeModel.TrappedMonitors(detected, [Both("a", "b", Wall)]).ToList());
            Assert.IsEmpty(CursorBrakeEdgeModel.Prune([Both("a", "b", Wall)], detected.Seen, out _));
            Assert.IsTrue(detected.Edges.TrueForAll(e => e.Monitors.Count == 2), "An edge without two monitors is dropped.");
        }

        [TestMethod]
        public void TheRemovalDateStopsAtTheEndOfTheCalendar()
        {
            Assert.AreEqual(new System.DateTime(2027, 3, 31, 8, 0, 0, System.DateTimeKind.Utc), CursorBrakeEdgeModel.RemovedAfter(new System.DateTime(2026, 10, 2, 8, 0, 0, System.DateTimeKind.Utc)));
            Assert.IsNull(CursorBrakeEdgeModel.RemovedAfter(new System.DateTime(9999, 12, 31, 0, 0, 0, System.DateTimeKind.Utc)), "A stamp the calendar cannot carry 180 days further has no removal date.");
        }

        // The module publishes Windows' numbers, which need not follow the main display or reading
        // order. A file without them still numbers every monitor, after the highest published
        // number so none repeats.
        [TestMethod]
        [DataRow(new[] { 2, 3, 1 }, new[] { 2, 3, 1 }, "published")]
        [DataRow(new[] { 0, 0, 0 }, new[] { 1, 2, 3 }, "none published")]
        [DataRow(new[] { 0, 1, 0 }, new[] { 2, 1, 3 }, "some published")]
        public void MonitorsShowTheNumbersTheModulePublished(int[] published, int[] expected, string name)
        {
            var detected = Detected();
            for (int i = 0; i < published.Length; i++)
            {
                detected.Monitors[i].Number = published[i];
            }

            CollectionAssert.AreEqual(expected, CursorBrakeEdgeModel.Numbers(detected).ToArray(), name);
        }
    }
}
