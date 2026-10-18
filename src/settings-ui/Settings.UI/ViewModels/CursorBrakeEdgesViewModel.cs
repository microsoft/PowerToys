// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// The CursorBrake edge editor: one row per edge the module detected, the displays with
    /// settings that are not connected now, and the trap caution. Edits go straight into the
    /// module's <c>edges</c> setting, as every other CursorBrake setting does.
    /// </summary>
    public partial class CursorBrakeEdgesViewModel : Observable
    {
        public const string ShowEdgesAction = "show_edges";

        private readonly CursorBrakeProperties _properties;
        private readonly IFileSystem _fileSystem;
        private readonly string _detectedEdgesPath;
        private readonly Action _save;
        private readonly Action<string> _sendCustomAction;
        private readonly Func<string, string> _resource;

        private CursorBrakeDetectedEdges _detected;
        private List<int> _numbers = new List<int>();
        private int _globalPushDistance = CursorBrakeProperties.DefaultPushDistance;
        private CursorBrakeEdgeFilter _edgeFilter;
        private CursorBrakeCrossModifier _crossModifier;
        private string _trapTitle = string.Empty;
        private string _trapMessage = string.Empty;
        private CursorBrakeEdgeRowViewModel _selectedRow;

        /// <param name="properties">The CursorBrake settings the edits go into.</param>
        /// <param name="fileSystem">Where <paramref name="detectedEdgesPath"/> is read.</param>
        /// <param name="detectedEdgesPath">The module's detected-edges.json.</param>
        /// <param name="save">Saves the settings and sends them to the module.</param>
        /// <param name="sendCustomAction">Sends a custom action to the module.</param>
        /// <param name="resource">Returns the localized string for a resource name.</param>
        public CursorBrakeEdgesViewModel(CursorBrakeProperties properties, IFileSystem fileSystem, string detectedEdgesPath, Action save, Action<string> sendCustomAction, Func<string, string> resource)
        {
            _properties = properties ?? throw new ArgumentNullException(nameof(properties));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _detectedEdgesPath = detectedEdgesPath;
            _save = save;
            _sendCustomAction = sendCustomAction;
            _resource = resource;

            // The editor shows what the module does, so it starts from the entries the module reads.
            // Settings never fails on a malformed entry, and only an edit saves the cleaned list.
            _properties.Edges ??= new GenericProperty<List<CursorBrakeEdgeSetting>>(new List<CursorBrakeEdgeSetting>());
            _properties.Edges.Value = CursorBrakeEdgeModel.Sanitize(_properties.Edges.Value);
        }

        public ObservableCollection<CursorBrakeEdgeRowViewModel> Rows { get; } = new ObservableCollection<CursorBrakeEdgeRowViewModel>();

        public ObservableCollection<CursorBrakeDisconnectedEdgeViewModel> DisconnectedRows { get; } = new ObservableCollection<CursorBrakeDisconnectedEdgeViewModel>();

        /// <summary>Gets what the module published last, or null before it has.</summary>
        public CursorBrakeDetectedEdges Detected => _detected;

        /// <summary>Gets the number the editor gives each display, by index.</summary>
        public IReadOnlyList<int> Numbers => _numbers;

        /// <summary>Gets a value indicating whether there are edges to edit.</summary>
        public bool HasDetectedEdges => _detected != null && _detected.Monitors.Count >= 2;

        /// <summary>Gets a value indicating whether only one display is connected, so there is no edge.</summary>
        public bool IsSingleDisplay => _detected != null && _detected.Monitors.Count < 2;

        /// <summary>Gets a value indicating whether some display has no ID, so its edges cannot carry settings.</summary>
        public bool IsUnidentified => _detected != null && !_detected.Identified;

        public bool HasDisconnectedRows => DisconnectedRows.Count > 0;

        public string TrapTitle
        {
            get => _trapTitle;
            private set => Set(ref _trapTitle, value);
        }

        public string TrapMessage
        {
            get => _trapMessage;
            private set => Set(ref _trapMessage, value);
        }

        public bool IsTrapVisible => !string.IsNullOrEmpty(_trapTitle);

        /// <summary>Gets the editor's description, which names the key that crosses a wall.</summary>
        public string EditorDescription => Format("CursorBrake_Edges_Description", CrossKeyName);

        /// <summary>Gets the legend's wall entry, which names the key that crosses a wall.</summary>
        public string WallLegend => Format("CursorBrake_Edges_WallLegend", CrossKeyName);

        /// <summary>Gets the name of the key that crosses a wall, as Settings shows it.</summary>
        public string CrossKeyName => _resource(_crossModifier == CursorBrakeCrossModifier.Shift ? "CursorBrake_Key_Shift" : "CursorBrake_Key_Ctrl");

        /// <summary>Gets or sets the edge selected on the map or in the list.</summary>
        public CursorBrakeEdgeRowViewModel SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (Set(ref _selectedRow, value))
                {
                    foreach (var row in Rows)
                    {
                        row.IsSelected = row == value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets what the editor's expander lists, in order: this view model for the map, then each
        /// edge's row followed by its two direction rows while it is split, then the "Disconnected
        /// displays" heading as a string and the disconnected pairs.
        /// </summary>
        public ObservableCollection<object> ListItems { get; } = new ObservableCollection<object>();

        /// <summary>Gets or sets the global strength, which edges left at Default use.</summary>
        public int GlobalPushDistance
        {
            get => _globalPushDistance;
            set
            {
                if (Set(ref _globalPushDistance, value))
                {
                    RefreshRows();
                }
            }
        }

        /// <summary>Gets or sets the cross key.</summary>
        public CursorBrakeCrossModifier CrossModifier
        {
            get => _crossModifier;
            set
            {
                if (Set(ref _crossModifier, value))
                {
                    OnPropertyChanged(nameof(CrossKeyName));
                    OnPropertyChanged(nameof(EditorDescription));
                    OnPropertyChanged(nameof(WallLegend));
                    RefreshRows();
                }
            }
        }

        /// <summary>Gets or sets which edges left at Default resist the cursor.</summary>
        public CursorBrakeEdgeFilter EdgeFilter
        {
            get => _edgeFilter;
            set
            {
                if (Set(ref _edgeFilter, value))
                {
                    RefreshRows();
                }
            }
        }

        private List<CursorBrakeEdgeSetting> Edges => _properties.Edges.Value;

        /// <summary>
        /// Reads detected-edges.json again. A file that cannot be read, such as one the module is
        /// still writing, leaves the editor as it was, because the write's own change notice
        /// follows. A missing file clears the editor. Entries for pairs the module does not
        /// remember are dropped, and the settings are saved only when that removed something.
        /// Dropping waits until the module has published since the settings were last written,
        /// because the module stamps a pair an entry names only when it reads the entry.
        /// </summary>
        public void Reload()
        {
            CursorBrakeDetectedEdges detected = null;
            try
            {
                if (_fileSystem.File.Exists(_detectedEdgesPath))
                {
                    detected = JsonSerializer.Deserialize(_fileSystem.File.ReadAllText(_detectedEdgesPath), SettingsSerializationContext.Default.CursorBrakeDetectedEdges);
                    if (detected == null || detected.Version != CursorBrakeDetectedEdges.CurrentVersion)
                    {
                        Logger.LogWarning($"Ignoring CursorBrake detected edges of version {detected?.Version}.");
                        return;
                    }

                    detected = CursorBrakeEdgeModel.Clean(detected);
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                Logger.LogInfo($"CursorBrake detected edges could not be read yet: {ex.Message}");
                return;
            }

            // The module rewrites the file whole on its next publish, so content no check above
            // foresaw leaves the editor empty until then rather than ending Settings.
            try
            {
                // The module publishes after every save, mostly with the layout unchanged. Rows a
                // user may be clicking on are then kept, and only their values refresh.
                bool sameLayout = SameLayout(_detected, detected);
                _detected = detected;
                if (_detected != null && PublishedSinceSettings())
                {
                    var pruned = CursorBrakeEdgeModel.Prune(Edges, _detected.Seen, out bool changed);
                    if (changed)
                    {
                        _properties.Edges.Value = pruned;
                        _save();
                    }
                }

                if (sameLayout)
                {
                    RefreshRows();
                }
                else
                {
                    Rebuild();
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogError($"CursorBrake could not show the detected edges, so the editor stays empty: {ex}");
                _detected = null;
                Rebuild();
            }
        }

        // Whether two publishes describe the same monitors and edges, which is all the rows show.
        private static bool SameLayout(CursorBrakeDetectedEdges a, CursorBrakeDetectedEdges b)
        {
            if (a == null || b == null || a.Identified != b.Identified || a.Monitors.Count != b.Monitors.Count || a.Edges.Count != b.Edges.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Monitors.Count; i++)
            {
                var x = a.Monitors[i];
                var y = b.Monitors[i];
                if (x.Id != y.Id || x.Name != y.Name || !x.Rect.SequenceEqual(y.Rect) || x.Dpi != y.Dpi || x.Primary != y.Primary || x.Number != y.Number || x.BuiltIn != y.BuiltIn)
                {
                    return false;
                }
            }

            for (int i = 0; i < a.Edges.Count; i++)
            {
                var x = a.Edges[i];
                var y = b.Edges[i];
                if (!x.Monitors.SequenceEqual(y.Monitors) || x.Axis != y.Axis || x.At != y.At || x.From != y.From || x.To != y.To)
                {
                    return false;
                }
            }

            return true;
        }

        // Whether detected-edges.json is at least as new as settings.json beside it.
        private bool PublishedSinceSettings()
        {
            string settingsPath = Path.Combine(Path.GetDirectoryName(_detectedEdgesPath) ?? string.Empty, SettingsUtils.DefaultFileName);
            return !_fileSystem.File.Exists(settingsPath)
                || _fileSystem.File.GetLastWriteTimeUtc(_detectedEdgesPath) >= _fileSystem.File.GetLastWriteTimeUtc(settingsPath);
        }

        public void ShowOnScreens()
        {
            _sendCustomAction(ShowEdgesAction);
        }

        public void Forget(CursorBrakeDisconnectedEdgeViewModel row)
        {
            ArgumentNullException.ThrowIfNull(row);
            _properties.Edges.Value = Edges.Where(e => !(e.Monitors.Count == 2 && CursorBrakeEdgeModel.IsSamePair(e.Monitors[0], e.Monitors[1], row.First, row.Second))).ToList();
            _save();
            RefreshDisconnectedRows();
        }

        private void Rebuild()
        {
            Rows.Clear();
            DisconnectedRows.Clear();
            SelectedRow = null;
            _numbers = _detected == null ? new List<int>() : CursorBrakeEdgeModel.Numbers(_detected).ToList();

            if (_detected != null)
            {
                for (int i = 0; i < _detected.Edges.Count; i++)
                {
                    var edge = _detected.Edges[i];
                    if (edge.Monitors.Count != 2 || edge.Monitors.Any(m => m < 0 || m >= _detected.Monitors.Count) || edge.Monitors[0] == edge.Monitors[1])
                    {
                        continue;
                    }

                    int first = edge.Monitors[0];
                    int second = edge.Monitors[1];
                    bool editable = !string.IsNullOrEmpty(_detected.Monitors[first].Id) && !string.IsNullOrEmpty(_detected.Monitors[second].Id);
                    CursorBrakeEdgeRowViewModel row = null;
                    string letter = CursorBrakeEdgeRowViewModel.LetterOf(i);
                    var forward = new CursorBrakeEdgeDirectionViewModel(first, second, Parts("CursorBrake_Edges_DirectionHeader", Named(first), Named(second)), "MouseUtils_CursorBrakeEdgeForward_" + letter, () => OnDirectionEdited(row));
                    var backward = new CursorBrakeEdgeDirectionViewModel(second, first, Parts("CursorBrake_Edges_DirectionHeader", Named(second), Named(first)), "MouseUtils_CursorBrakeEdgeBackward_" + letter, () => OnDirectionEdited(row));
                    row = new CursorBrakeEdgeRowViewModel(i, first, second, Parts("CursorBrake_Edges_PairTitle", Named(first), Named(second)), editable, forward, backward, OnRowEdited, selected => SelectedRow = selected);
                    Rows.Add(row);
                }
            }

            RefreshRows();
            OnPropertyChanged(nameof(Detected));
            OnPropertyChanged(nameof(Numbers));
            OnPropertyChanged(nameof(HasDetectedEdges));
            OnPropertyChanged(nameof(IsSingleDisplay));
            OnPropertyChanged(nameof(IsUnidentified));
        }

        // Shows every row as the settings now read, keeping a row the user split while it is open.
        private void RefreshRows()
        {
            foreach (var row in Rows)
            {
                bool braked = DefaultBrakes(_detected.Edges[row.EdgeIndex]);
                var rowItems = RowItems(braked);
                var directionItems = rowItems.Take(CursorBrakeEdgeRowViewModel.DifferentEachWayItem).ToList();
                var forward = Value(row.First, row.Second);
                var backward = Value(row.Second, row.First);
                bool split = row.IsSplit || forward != backward;
                row.Forward.ShowChoice(ItemOf(forward), DistanceOf(forward), directionItems, braked);
                row.Backward.ShowChoice(ItemOf(backward), DistanceOf(backward), directionItems, braked);
                row.Show(split ? CursorBrakeEdgeRowViewModel.DifferentEachWayItem : ItemOf(forward), DistanceOf(forward), split, rowItems, braked);
                row.DescriptionParts = DescribeEdge(row, forward, backward);
            }

            RefreshTrap();
            RefreshDisconnectedRows();
        }

        // Brings ListItems in line with the rows, inserting and removing in place so a control that
        // holds focus inside a row keeps it.
        private void SyncListItems()
        {
            var desired = new List<object>();
            if (HasDetectedEdges)
            {
                desired.Add(this);
                foreach (var row in Rows)
                {
                    desired.Add(row);
                    if (row.IsSplit)
                    {
                        desired.Add(row.Forward);
                        desired.Add(row.Backward);
                    }
                }
            }

            if (DisconnectedRows.Count > 0)
            {
                desired.Add(_resource("MouseUtils_CursorBrake_Edges_Disconnected"));
                desired.AddRange(DisconnectedRows);
            }

            for (int i = 0; i < desired.Count; i++)
            {
                if (i < ListItems.Count && Same(ListItems[i], desired[i]))
                {
                    continue;
                }

                int found = -1;
                for (int j = i + 1; j < ListItems.Count; j++)
                {
                    if (Same(ListItems[j], desired[i]))
                    {
                        found = j;
                        break;
                    }
                }

                if (found < 0)
                {
                    ListItems.Insert(i, desired[i]);
                }
                else
                {
                    for (int k = i; k < found; k++)
                    {
                        ListItems.RemoveAt(i);
                    }
                }
            }

            while (ListItems.Count > desired.Count)
            {
                ListItems.RemoveAt(ListItems.Count - 1);
            }
        }

        // The heading is a string, which compares by value; everything else by reference.
        private static bool Same(object a, object b)
        {
            return a is string text ? text.Equals(b) : ReferenceEquals(a, b);
        }

        private void OnRowEdited(CursorBrakeEdgeRowViewModel row)
        {
            if (row.SelectedIndex == CursorBrakeEdgeRowViewModel.DifferentEachWayItem)
            {
                // Splitting starts from the values the edge has, so nothing changes until a
                // direction does.
                SyncListItems();
                return;
            }

            var value = new CursorBrakeEdgeValue(row.Mode, row.PushDistance);
            Write(row, value, value);
        }

        private void OnDirectionEdited(CursorBrakeEdgeRowViewModel row)
        {
            var forward = new CursorBrakeEdgeValue(row.Forward.Mode, row.Forward.PushDistance);
            var backward = new CursorBrakeEdgeValue(row.Backward.Mode, row.Backward.PushDistance);
            Write(row, forward, backward);
        }

        private void Write(CursorBrakeEdgeRowViewModel row, CursorBrakeEdgeValue forward, CursorBrakeEdgeValue backward)
        {
            if (_detected == null || !row.IsEditable)
            {
                return;
            }

            string first = _detected.Monitors[row.First].Id;
            string second = _detected.Monitors[row.Second].Id;
            var edges = CursorBrakeEdgeModel.Normalize(CursorBrakeEdgeModel.SetPair(Edges, first, second, forward, backward));

            // Each save makes the module publish, so an edit that leaves the entries as they were
            // saves nothing.
            if (!SameEntries(edges, Edges))
            {
                _properties.Edges.Value = edges;
                _save();
            }

            RefreshRows();
        }

        private static bool SameEntries(List<CursorBrakeEdgeSetting> a, List<CursorBrakeEdgeSetting> b)
        {
            return a.Count == b.Count && a.Zip(b).All(p =>
                p.First.Monitors.SequenceEqual(p.Second.Monitors, StringComparer.Ordinal)
                && p.First.OneWay == p.Second.OneWay
                && p.First.Mode == p.Second.Mode
                && p.First.PushDistance == p.Second.PushDistance);
        }

        private CursorBrakeEdgeValue Value(int from, int to)
        {
            return CursorBrakeEdgeModel.DirectionValue(_detected, Edges, from, to);
        }

        private int DistanceOf(CursorBrakeEdgeValue value)
        {
            return value.Mode == CursorBrakeEdgeMode.Default ? _globalPushDistance : value.PushDistance;
        }

        private static int ItemOf(CursorBrakeEdgeValue value)
        {
            return CursorBrakeEdgeChoiceViewModel.ItemOf(value.Mode);
        }

        // Whether Default brakes the edge: Apply to leaves stacked edges out of "Side-by-side only"
        // and side-by-side ones out of "Stacked only", as the module's RuleFor does.
        private bool DefaultBrakes(CursorBrakeDetectedEdge edge)
        {
            return edge.IsSideBySide ? _edgeFilter != CursorBrakeEdgeFilter.Stacked : _edgeFilter != CursorBrakeEdgeFilter.SideBySide;
        }

        // The items of an edge's ComboBox. Default names what it does on the edge: the global
        // strength where Apply to brakes the edge, and Off where it leaves the edge out.
        private List<string> RowItems(bool braked)
        {
            return new List<string>
            {
                braked ? Format("CursorBrake_Edges_DefaultItem", _globalPushDistance) : _resource("CursorBrake_Edges_DefaultOffItem"),
                _resource("CursorBrake_Edges_CustomItem"),
                _resource("CursorBrake_Edges_OffItem"),
                _resource("CursorBrake_Edges_WallItem"),
                _resource("CursorBrake_Edges_DifferentEachWayItem"),
            };
        }

        // "Side by side · 1080 px shared · 100% and 150%", plus the wall and its key when exactly
        // one direction is a wall, with the wall's monitors as icons.
        private List<CursorBrakeTextPart> DescribeEdge(CursorBrakeEdgeRowViewModel row, CursorBrakeEdgeValue forward, CursorBrakeEdgeValue backward)
        {
            var edge = _detected.Edges[row.EdgeIndex];
            var parts = new List<string>
            {
                _resource(edge.IsSideBySide ? "CursorBrake_Edges_SideBySide" : "CursorBrake_Edges_Stacked"),
                Format("CursorBrake_Edges_Shared", edge.To - edge.From),
                Format("CursorBrake_Edges_And", ScaleText(_detected.Monitors[row.First]), ScaleText(_detected.Monitors[row.Second])),
            };

            bool forwardWall = forward.Mode == CursorBrakeEdgeMode.Wall;
            bool backwardWall = backward.Mode == CursorBrakeEdgeMode.Wall;
            if (forwardWall != backwardWall)
            {
                int from = forwardWall ? row.First : row.Second;
                int to = forwardWall ? row.Second : row.First;
                return Merge([Text(string.Join(" · ", parts) + " · "), .. Parts("CursorBrake_Edges_WallNote", IconOnly(from), IconOnly(to), CrossKeyName)]);
            }

            return [Text(string.Join(" · ", parts))];
        }

        private void RefreshTrap()
        {
            int trapped = _detected == null ? -1 : CursorBrakeEdgeModel.TrappedMonitors(_detected, Edges).DefaultIfEmpty(-1).First();
            if (trapped < 0)
            {
                TrapTitle = string.Empty;
                TrapMessage = string.Empty;
            }
            else
            {
                var entries = CursorBrakeEdgeModel.WaysIn(_detected, Edges, trapped)
                    .Select(n => _numbers[n])
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();
                TrapTitle = Format("CursorBrake_Edges_TrapTitle", Label(trapped), CrossKeyName);
                TrapMessage = Format("CursorBrake_Edges_TrapMessage", JoinAnd(entries.Select(n => n.ToString(CultureInfo.CurrentCulture)).ToList()));
            }

            OnPropertyChanged(nameof(IsTrapVisible));
        }

        // Keeps the rows when nothing they show changed, so a Forget button a user is about to
        // click stays the same button.
        private void RefreshDisconnectedRows()
        {
            var rows = new List<CursorBrakeDisconnectedEdgeViewModel>();
            var pairs = new List<(string First, string Second)>();
            foreach (var entry in Edges)
            {
                if (entry.Monitors.Count != 2 || string.IsNullOrEmpty(entry.Monitors[0]) || string.IsNullOrEmpty(entry.Monitors[1]))
                {
                    continue;
                }

                string first = entry.Monitors[0];
                string second = entry.Monitors[1];
                if (pairs.Any(p => CursorBrakeEdgeModel.IsSamePair(p.First, p.Second, first, second)) || IsDetectedPair(first, second))
                {
                    continue;
                }

                pairs.Add((first, second));
                rows.Add(new CursorBrakeDisconnectedEdgeViewModel(first, second, Format("CursorBrake_Edges_PairTitle", LabelOrCode(first), LabelOrCode(second)), DescribeDisconnected(first, second), Forget));
            }

            bool same = rows.Count == DisconnectedRows.Count && rows.Zip(DisconnectedRows).All(p =>
                p.First.First == p.Second.First && p.First.Second == p.Second.Second && p.First.Title == p.Second.Title && p.First.Description == p.Second.Description);
            if (!same)
            {
                DisconnectedRows.Clear();
                foreach (var row in rows)
                {
                    DisconnectedRows.Add(row);
                }
            }

            OnPropertyChanged(nameof(HasDisconnectedRows));
            SyncListItems();
        }

        private bool IsDetectedPair(string first, string second)
        {
            return _detected != null && _detected.Edges.Any(e =>
                e.Monitors.Count == 2
                && e.Monitors.All(m => m >= 0 && m < _detected.Monitors.Count)
                && CursorBrakeEdgeModel.IsSamePair(_detected.Monitors[e.Monitors[0]].Id, _detected.Monitors[e.Monitors[1]].Id, first, second));
        }

        // "Off · Last connected 10/2/2026 · Removed after 3/31/2027".
        private string DescribeDisconnected(string first, string second)
        {
            var items = RowItems(true);
            var forward = CursorBrakeEdgeModel.Lookup(Edges, first, second);
            var backward = CursorBrakeEdgeModel.Lookup(Edges, second, first);
            var parts = new List<string>();
            if (forward == backward)
            {
                parts.Add(items[ItemOf(forward)]);
            }
            else
            {
                parts.Add($"{Format("CursorBrake_Edges_DirectionHeader", LabelOrCode(first), LabelOrCode(second))}: {items[ItemOf(forward)]}");
                parts.Add($"{Format("CursorBrake_Edges_DirectionHeader", LabelOrCode(second), LabelOrCode(first))}: {items[ItemOf(backward)]}");
            }

            var seen = _detected?.Seen.FirstOrDefault(s => CursorBrakeEdgeModel.IsSamePair(s.Monitors[0], s.Monitors[1], first, second));
            if (seen != null && DateTime.TryParse(seen.LastSeenUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var lastSeen))
            {
                parts.Add(Format("CursorBrake_Edges_LastConnected", lastSeen.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)));
                if (CursorBrakeEdgeModel.RemovedAfter(lastSeen) is DateTime removal)
                {
                    parts.Add(Format("CursorBrake_Edges_Kept", removal.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)));
                }
            }

            return string.Join(" · ", parts);
        }

        // "Dell U2724D (3)", or "Monitor 3" for a monitor without a name.
        private string Label(int monitor)
        {
            string name = _detected.Monitors[monitor].Name;
            int number = _numbers[monitor];
            return string.IsNullOrEmpty(name) ? Format("CursorBrake_Edges_DisplayFallback", number) : Format("CursorBrake_Edges_DisplayLabel", name, number);
        }

        // A connected display by its label, any other by the model code in its ID.
        private string LabelOrCode(string id)
        {
            int index = _detected == null ? -1 : _detected.Monitors.FindIndex(m => CursorBrakeEdgeModel.IsSameId(m.Id, id));
            if (index >= 0)
            {
                return Label(index);
            }

            var parts = id.Split('#');
            return parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]) ? parts[1] : id;
        }

        /// <summary>A monitor's scaling as Settings shows it, such as "150%".</summary>
        internal static string ScaleText(CursorBrakeDetectedMonitor monitor)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0}%", monitor.Dpi * 100 / 96);
        }

        private string JoinAnd(IReadOnlyList<string> items)
        {
            if (items.Count == 0)
            {
                return string.Empty;
            }

            if (items.Count == 1)
            {
                return items[0];
            }

            return Format("CursorBrake_Edges_And", string.Join(", ", items.Take(items.Count - 1)), items[^1]);
        }

        private string Format(string resourceName, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, _resource(resourceName), args);
        }

        private static CursorBrakeTextPart Text(string text) => new(text, text);

        // A monitor as its icon and name; the plain form is its label, "Dell U2724D (3)".
        private List<CursorBrakeTextPart> Named(int monitor)
        {
            string name = _detected.Monitors[monitor].Name;
            string shown = string.IsNullOrEmpty(name) ? Format("CursorBrake_Edges_DisplayFallback", _numbers[monitor]) : name;
            return [new CursorBrakeTextPart(shown, Label(monitor), _numbers[monitor], _detected.Monitors[monitor].BuiltIn)];
        }

        // A monitor as its icon alone; the plain form is its number.
        private List<CursorBrakeTextPart> IconOnly(int monitor)
        {
            return [new CursorBrakeTextPart(string.Empty, _numbers[monitor].ToString(CultureInfo.CurrentCulture), _numbers[monitor], _detected.Monitors[monitor].BuiltIn)];
        }

        // A localized format split on its placeholders, so translators keep the word order. An
        // argument that is a list of parts goes in as those parts; any other is formatted as text.
        private List<CursorBrakeTextPart> Parts(string resourceName, params object[] args)
        {
            string format = _resource(resourceName);
            var parts = new List<CursorBrakeTextPart>();
            int position = 0;
            foreach (Match match in PlaceholderPattern().Matches(format))
            {
                parts.Add(Text(format[position..match.Index]));
                int index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                if (index < args.Length && args[index] is IEnumerable<CursorBrakeTextPart> inserted)
                {
                    parts.AddRange(inserted);
                }
                else
                {
                    parts.Add(Text(index < args.Length ? Convert.ToString(args[index], CultureInfo.CurrentCulture) ?? string.Empty : match.Value));
                }

                position = match.Index + match.Length;
            }

            parts.Add(Text(format[position..]));
            return Merge(parts);
        }

        // Joins neighboring text parts and drops empty ones, so a line has one part per run of text.
        private static List<CursorBrakeTextPart> Merge(IEnumerable<CursorBrakeTextPart> parts)
        {
            var merged = new List<CursorBrakeTextPart>();
            foreach (var part in parts)
            {
                if (!part.IsMonitor && part.Text.Length == 0)
                {
                    continue;
                }

                if (!part.IsMonitor && merged.Count > 0 && !merged[^1].IsMonitor)
                {
                    merged[^1] = Text(merged[^1].Text + part.Text);
                }
                else
                {
                    merged.Add(part);
                }
            }

            return merged;
        }

        [GeneratedRegex(@"\{(\d+)\}")]
        private static partial Regex PlaceholderPattern();
    }
}
