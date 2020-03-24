using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Search.Interop;

namespace Wox.Plugin.Indexer.SearchHelper
{
    class DriveDetection
    {
        // Variable which sets the warning status, can be turned off by user
        // TODO : To be linked with the UI once it is finalized
        public bool warningOn = true;

        // Function to return the names of all drives
        public List<string> GetDrives()
        {
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            List<string> allDriveNames = new List<string>();

            foreach (DriveInfo d in allDrives)
            {
                allDriveNames.Add(d.Name);
            }
            return allDriveNames;
        }

        [STAThread]
        // Function to get all the Search Scopes of the indexer
        public List<string> GetScopeRules()
        {
            List<string> allScopeRules = new List<string>();
            // This uses the Microsoft.Search.Interop assembly
            CSearchManager manager = new CSearchManager();

            // SystemIndex catalog is the default catalog in Windows
            ISearchCatalogManager catalogManager = manager.GetCatalog("SystemIndex");

            // Get the ISearchQueryHelper which will help us to translate AQS --> SQL necessary to query the indexer
            ISearchCrawlScopeManager crawlScopeManager = catalogManager.GetCrawlScopeManager();

            // search for the scope rules
            IEnumSearchScopeRules scopeRules = crawlScopeManager.EnumerateScopeRules();
            CSearchScopeRule scopeRule;
            uint numScopes = 0;

            bool nextExists = true;
            while (nextExists)
            {
                try
                {
                    scopeRules.Next(1, out scopeRule, ref numScopes);
                    allScopeRules.Add(scopeRule.PatternOrURL);
                    /*Console.WriteLine(numScopes);*/
                }
                catch (Exception ex)
                {
                    nextExists = false;
                }
            }

            return allScopeRules;
        }

        // Function to check if all Drives are indexed
        public bool allDrivesIndexed()
        {
            bool allDrivesAreIndexed = true;
            List<string> drives = GetDrives();
            List<string> scopeRules = GetScopeRules();

            foreach (var drive in drives)
            {
                string driveScope = @"file:///" + drive;
                if (!scopeRules.Contains(driveScope))
                {
                    allDrivesAreIndexed = false;
                    break;
                }
            }

            return allDrivesAreIndexed;
        }

    }
}
