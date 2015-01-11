using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Wox.Core;
using Wox.Core.UserSettings;
using Wox.Core.Version;
using Wox.Helper;
using Wox.Infrastructure;
using Wox.Infrastructure.Http;

namespace Wox.Update
{
    public class UpdateChecker
    {
        private static Release newRelease;
        private static bool checkedUpdate = false;

        /// <summary>
        /// If new release is available, then return the new release
        /// otherwise, return null
        /// </summary>
        /// <returns></returns>
        public Release CheckUpgrade(bool forceCheck = false)
        {
            if (checkedUpdate && !forceCheck) return newRelease;
            string json = HttpRequest.Get(APIServer.LastestReleaseURL,HttpProxy.Instance);
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                newRelease = JsonConvert.DeserializeObject<Release>(json);
                if (!IsNewerThanCurrent(newRelease))
                {
                    newRelease = null;
                }
                checkedUpdate = true;
            }
            catch{}

            return newRelease;
        }

        private bool IsNewerThanCurrent(Release release)
        {
            if (release == null) return false;

            return new SemanticVersion(release.version) > VersionManager.Instance.CurrentVersion;
        }
    }
}
