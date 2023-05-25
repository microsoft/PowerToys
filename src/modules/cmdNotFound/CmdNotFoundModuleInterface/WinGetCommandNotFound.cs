using System.Security.Principal;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Feedback;
using System.Runtime.InteropServices;
using Microsoft.Management.Deployment;

namespace wingetprovider
{
    // Adapted from https://github.com/microsoft/winget-cli/blob/1898da0b657585d2e6399ef783ecb667eed280f9/src/PowerShell/Microsoft.WinGet.Client/Helpers/ComObjectFactory.cs
    public class ComObjectFactory
    {
        private static readonly Guid PackageManagerClsid = Guid.Parse("C53A4F16-787E-42A4-B304-29EFFB4BF597");
        private static readonly Guid FindPackagesOptionsClsid = Guid.Parse("572DED96-9C60-4526-8F92-EE7D91D38C1A");
        private static readonly Guid PackageMatchFilterClsid = Guid.Parse("D02C9DAF-99DC-429C-B503-4E504E4AB000");

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "COM only usage.")]
        private static readonly Type PackageManagerType = Type.GetTypeFromCLSID(PackageManagerClsid);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "COM only usage.")]
        private static readonly Type FindPackagesOptionsType = Type.GetTypeFromCLSID(FindPackagesOptionsClsid);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "COM only usage.")]
        private static readonly Type PackageMatchFilterType = Type.GetTypeFromCLSID(PackageMatchFilterClsid);

        private static readonly Guid PackageManagerIid = Guid.Parse("B375E3B9-F2E0-5C93-87A7-B67497F7E593");
        private static readonly Guid FindPackagesOptionsIid = Guid.Parse("A5270EDD-7DA7-57A3-BACE-F2593553561F");
        private static readonly Guid PackageMatchFilterIid = Guid.Parse("D981ECA3-4DE5-5AD7-967A-698C7D60FC3B");

        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "COM only usage.")]
        private static T Create<T>(Type type, in Guid iid)
        {
            object instance = null;
            if (IsAdministrator())
            {
                var hr = WinGetServerManualActivation_CreateInstance(type.GUID, iid, 0, out instance);
                if (hr < 0)
                {
                    throw new COMException($"Failed to create instance: {hr}", hr);
                }
            }
            else
            {
                instance = Activator.CreateInstance(type);
            }

            IntPtr pointer = Marshal.GetIUnknownForObject(instance);
            return WinRT.MarshalInterface<T>.FromAbi(pointer);
        }

        [DllImport("winrtact.dll", EntryPoint = "WinGetServerManualActivation_CreateInstance", ExactSpelling = true, PreserveSig = true)]
        private static extern int WinGetServerManualActivation_CreateInstance(
                [In, MarshalAs(UnmanagedType.LPStruct)] Guid clsid,
                [In, MarshalAs(UnmanagedType.LPStruct)] Guid iid,
                uint flags,
                [Out, MarshalAs(UnmanagedType.IUnknown)] out object instance);

        [DllImport("winrtact.dll", EntryPoint = "winrtact_Initialize", ExactSpelling = true, PreserveSig = true)]
        public static extern void InitializeUndockedRegFreeWinRT();

        public static PackageManager CreatePackageManager()
        {
            return Create<PackageManager>(PackageManagerType, PackageManagerIid);
        }

        public static FindPackagesOptions CreateFindPackagesOptions()
        {
            return Create<FindPackagesOptions>(FindPackagesOptionsType, FindPackagesOptionsIid);
        }

        public static PackageMatchFilter CreatePackageMatchFilter()
        {
            return Create<PackageMatchFilter>(PackageMatchFilterType, PackageMatchFilterIid);
        }
    }

    public sealed class WinGetComObjects
    {
        public static WinGetComObjects Singleton { get; } = new WinGetComObjects();

        private WinGetComObjects()
        {
            ComObjectFactory.InitializeUndockedRegFreeWinRT();
            packageManager = ComObjectFactory.CreatePackageManager();
            findPackagesOptions = ComObjectFactory.CreateFindPackagesOptions();
            packageMatchFilter = ComObjectFactory.CreatePackageMatchFilter();
        }

        public PackageManager packageManager { get; }
        public FindPackagesOptions findPackagesOptions { get; }
        public PackageMatchFilter packageMatchFilter { get; }
    }

    public sealed class Init : IModuleAssemblyInitializer, IModuleAssemblyCleanup
    {
        internal const string id = "e5351aa4-dfde-4d4d-bf0f-1a2f5a37d8d6";

        public void OnImport()
        {
            if (!Platform.IsWindows)
            {
                return;
            }

            // Ensure WinGet is installed
            using (var rs = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault()))
            {
                rs.Open();
                var invocation = rs.SessionStateProxy.InvokeCommand;
                var winget = invocation.GetCommand("winget", CommandTypes.Application);
                if (winget is null)
                {
                    return;
                }
            }

            SubsystemManager.RegisterSubsystem<IFeedbackProvider, WinGetCommandNotFoundFeedbackPredictor>(WinGetCommandNotFoundFeedbackPredictor.Singleton);
        }

        public void OnRemove(PSModuleInfo psModuleInfo)
        {
            SubsystemManager.UnregisterSubsystem<IFeedbackProvider>(new Guid(id));
        }
    }

    public sealed class WinGetCommandNotFoundFeedbackPredictor : IFeedbackProvider
    {
        private readonly Guid _guid;
        private bool _tooManySuggestions;

        private static readonly byte _maxSuggestions = 5;

        public static WinGetCommandNotFoundFeedbackPredictor Singleton { get; } = new WinGetCommandNotFoundFeedbackPredictor(Init.id);
        private WinGetCommandNotFoundFeedbackPredictor(string guid)
        {
            _guid = new Guid(guid);
            _tooManySuggestions = false;
        }

        public void Dispose()
        {
        }

        public Guid Id => _guid;

        public string Name => "Windows Package Manager - WinGet";

        public string Description => "Finds missing commands that can be installed via WinGet.";

        /// <summary>
        /// Gets feedback based on the given commandline and error record.
        /// </summary>
        public FeedbackItem? GetFeedback(FeedbackContext context, CancellationToken token)
        {
            var lastError = context.LastError;
            if (lastError != null && lastError.FullyQualifiedErrorId == "CommandNotFoundException")
            {
                var target = (string)lastError.TargetObject;
                var pkgList = _FindPackages(target);
                if (pkgList.Count == 0)
                {
                    return null;
                }

                // Build list of suggestions
                var suggestionList = new List<string>();
                foreach (var pkg in pkgList)
                {
                    suggestionList.Add(String.Format("winget install --id {0}", pkg.Id));
                }

                // Build footer message
                var filterFieldString = WinGetComObjects.Singleton.packageMatchFilter.Field == PackageMatchField.Command ? "command" : "name";
                var footerMessage = _tooManySuggestions ?
                    String.Format("Additional results can be found using \"winget search --{0} {1}\"", filterFieldString, WinGetComObjects.Singleton.packageMatchFilter.Value) :
                    null;

                return new FeedbackItem(
                    "Try installing this package using winget:",
                    suggestionList,
                    footerMessage,
                    FeedbackDisplayLayout.Portrait
                );
            }
            return null;
        }

        private void _ApplyPackageMatchFilter(PackageMatchField field, PackageFieldMatchOption matchOption, string query)
        {
            // Configure filter
            WinGetComObjects.Singleton.packageMatchFilter.Field = field;
            WinGetComObjects.Singleton.packageMatchFilter.Option = matchOption;
            WinGetComObjects.Singleton.packageMatchFilter.Value = query;

            // Apply filter
            WinGetComObjects.Singleton.findPackagesOptions.ResultLimit = _maxSuggestions + 1u;
            WinGetComObjects.Singleton.findPackagesOptions.Filters.Clear();
            WinGetComObjects.Singleton.findPackagesOptions.Filters.Add(WinGetComObjects.Singleton.packageMatchFilter);
        }

        private List<CatalogPackage> _TryGetBestMatchingPackage(IReadOnlyList<MatchResult> matches)
        {
            var results = new List<CatalogPackage>();
            if (matches.Count == 1)
            {
                // One match --> return the package
                results.Add(matches.First().CatalogPackage);
            }
            else if (matches.Count > 1)
            {
                // Multiple matches --> display top 5 matches (prioritize best matches first)
                var bestExactMatches = new List<CatalogPackage>();
                var secondaryMatches = new List<CatalogPackage>();
                var tertiaryMatches = new List<CatalogPackage>();
                for (int i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    switch (match.MatchCriteria.Option)
                    {
                        case PackageFieldMatchOption.EqualsCaseInsensitive:
                        case PackageFieldMatchOption.Equals:
                            bestExactMatches.Add(match.CatalogPackage);
                            break;
                        case PackageFieldMatchOption.StartsWithCaseInsensitive:
                            secondaryMatches.Add(match.CatalogPackage);
                            break;
                        case PackageFieldMatchOption.ContainsCaseInsensitive:
                            tertiaryMatches.Add(match.CatalogPackage);
                            break;
                    }
                }

                // Now return the top _maxSuggestions
                while (results.Count < _maxSuggestions)
                {
                    if (bestExactMatches.Count > 0)
                    {
                        results.Add(bestExactMatches.First());
                        bestExactMatches.RemoveAt(0);
                    }
                    else if (secondaryMatches.Count > 0)
                    {
                        results.Add(secondaryMatches.First());
                        secondaryMatches.RemoveAt(0);
                    }
                    else if (tertiaryMatches.Count > 0)
                    {
                        results.Add(tertiaryMatches.First());
                        tertiaryMatches.RemoveAt(0);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            _tooManySuggestions = matches.Count > _maxSuggestions;
            return results;
        }

        // Adapted from WinGet sample documentation: https://github.com/microsoft/winget-cli/blob/master/doc/specs/%23888%20-%20Com%20Api.md#32-search
        private List<CatalogPackage> _FindPackages(string query)
        {
            // Get the package catalog
            var catalogRef = WinGetComObjects.Singleton.packageManager.GetPredefinedPackageCatalog(PredefinedPackageCatalog.OpenWindowsCatalog);
            var connectResult = catalogRef.Connect();
            byte retryCount = 0;
            while (connectResult.Status != ConnectResultStatus.Ok && retryCount < 3)
            {
                connectResult = catalogRef.Connect();
                retryCount++;
            }
            var catalog = connectResult.PackageCatalog;

            // Perform the query (search by command)
            _ApplyPackageMatchFilter(PackageMatchField.Command, PackageFieldMatchOption.StartsWithCaseInsensitive, query);
            var findPackagesResult = catalog.FindPackages(WinGetComObjects.Singleton.findPackagesOptions);
            var matches = findPackagesResult.Matches;
            var pkgList = _TryGetBestMatchingPackage(matches);
            if (pkgList.Count > 0)
            {
                return pkgList;
            }

            // No matches found when searching by command,
            // let's try again and search by name
            _ApplyPackageMatchFilter(PackageMatchField.Name, PackageFieldMatchOption.ContainsCaseInsensitive, query);

            // Perform the query (search by name)
            findPackagesResult = catalog.FindPackages(WinGetComObjects.Singleton.findPackagesOptions);
            matches = findPackagesResult.Matches;
            return _TryGetBestMatchingPackage(matches);
        }
    }
}