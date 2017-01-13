using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using Wox.Infrastructure.Logger;

namespace Wox.Infrastructure.Storage
{
    /// <summary>
    /// Stroage object using binary data
    /// Normally, it has better performance, but not readable
    /// You MUST mark implement class as Serializable
    /// </summary>
    public class BinaryStorage<T> : Storage<T>
    {
        public BinaryStorage(string filename)
        {
            FileSuffix = ".cache";
            DirectoryName = "Cache";
            DirectoryPath = Path.Combine(DirectoryPath, DirectoryName);
            FileName = filename;
            FilePath = Path.Combine(DirectoryPath, FileName + FileSuffix);

            ValidateDirectory();
        }

        public T TryLoad(T defaultData)
        {
            if (File.Exists(FilePath))
            {
                using (var stream = new FileStream(FilePath, FileMode.Open))
                {
                    if (stream.Length > 0)
                    {
                        var d = Deserialize(stream, defaultData);
                        return d;
                    }
                    else
                    {
                        stream.Close();
                        Save(defaultData);
                        return defaultData;
                    }
                }
            }
            else
            {
                Save(defaultData);
                return defaultData;
            }
        }

        private T Deserialize(FileStream stream, T defaultData)
        {
            //http://stackoverflow.com/questions/2120055/binaryformatter-deserialize-gives-serializationexception
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            BinaryFormatter binaryFormatter = new BinaryFormatter
            {
                AssemblyFormat = FormatterAssemblyStyle.Simple
            };

            try
            {
                var t = (T) binaryFormatter.Deserialize(stream);
                return t;
            }
            catch (System.Exception e)
            {
                Log.Error($"Broken cache file: {FilePath}");
                Log.Exception(e);
                stream.Close();
                return defaultData;
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            }
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            Assembly ayResult = null;
            string sShortAssemblyName = args.Name.Split(',')[0];
            Assembly[] ayAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly ayAssembly in ayAssemblies)
            {
                if (sShortAssemblyName == ayAssembly.FullName.Split(',')[0])
                {
                    ayResult = ayAssembly;
                    break;
                }
            }
            return ayResult;
        }

        public void Save(T data)
        {
            using (var stream = new FileStream(FilePath, FileMode.Create))
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter
                {
                    AssemblyFormat = FormatterAssemblyStyle.Simple
                };

                try
                {
                    binaryFormatter.Serialize(stream, data);
                }
                catch (SerializationException e)
                {
                    Log.Exception(e);
                }
            }
        }
    }
}
