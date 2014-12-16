using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Wox.Helper;
using Wox.Infrastructure;

namespace Wox.Update
{
    public class UpdateChecker
    {
        private const string updateURL = "https://api.getwox.com/release/latest/";
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

            HttpWebResponse response = HttpRequest.CreateGetHttpResponse(updateURL, HttpProxy.Instance);
            Stream s = response.GetResponseStream();
            if (s != null)
            {
                StreamReader reader = new StreamReader(s, Encoding.UTF8);
                string json = reader.ReadToEnd();
                try
                {
                    newRelease = JsonConvert.DeserializeObject<Release>(json);
                }
                catch
                {
                }
            }

            if (!IsNewerThanCurrent(newRelease))
            {
                newRelease = null;
            }

            checkedUpdate = true;
            return newRelease;
        }

        private bool IsNewerThanCurrent(Release release)
        {
            if (release == null) return false;

            string currentVersion = ConfigurationManager.AppSettings["version"];
            return CompareVersion(release.version, currentVersion) > 0;
        }

        /// <summary>
        /// if version1 > version2 return 1 
        /// else -1 
        /// </summary>
        /// <param name="version1"></param>
        /// <param name="version2"></param>
        /// <returns></returns>
        private int CompareVersion(string version1, string version2)
        {
            if (version1 == version2) return 0;
            if (string.IsNullOrEmpty(version1) || string.IsNullOrEmpty(version2)) return 0;

            //semantic version, e.g. 1.1.0
            List<int> version1List = version1.Split('.').Select(int.Parse).ToList();
            List<int> version2List = version2.Split('.').Select(int.Parse).ToList();

            if (version1List[0] > version2List[0])
            {
                return 1;
            }
            else if (version1List[0] == version2List[0])
            {
                if (version1List[1] > version2List[1])
                {
                    return 1;
                }
                else if (version1List[1] == version2List[1])
                {
                    if (version1List[2] > version2List[2])
                    {
                        return 1;
                    }
                }
            }

            return -1;
        }
    }
}
