using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Wox.Infrastructure
{
    public static class Helper
    {
        /// <summary>
        /// http://www.yinwang.org/blog-cn/2015/11/21/programming-philosophy
        /// </summary>
        public static T NonNull<T>(this T obj)
        {
            if (obj == null)
            {
                throw new NullReferenceException();
            }
            else
            {
                return obj;
            }
        }

        public static void RequireNonNull<T>(this T obj)
        {
            if (obj == null)
            {
                throw new NullReferenceException();
            }
        }

        public static void ValidateDataDirectory(string bundledDataDirectory, string dataDirectory)
        {
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            foreach (var bundledDataPath in Directory.GetFiles(bundledDataDirectory))
            {
                var data = Path.GetFileName(bundledDataPath);
                var dataPath = Path.Combine(dataDirectory, data.NonNull());
                if (!File.Exists(dataPath))
                {
                    File.Copy(bundledDataPath, dataPath);
                }
                else
                {
                    var time1 = new FileInfo(bundledDataPath).LastWriteTimeUtc;
                    var time2 = new FileInfo(dataPath).LastWriteTimeUtc;
                    if (time1 != time2)
                    {
                        File.Copy(bundledDataPath, dataPath, true);
                    }
                }
            }
        }

        public static void ValidateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static string Formatted<T>(this T t)
        {
            var formatted = JsonConvert.SerializeObject(
               t,
               Formatting.Indented,
               new StringEnumConverter()
           );
            return formatted;
        }
    }
}
