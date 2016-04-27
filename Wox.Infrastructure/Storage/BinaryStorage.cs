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
    public class BinaryStorage<T> : Storage<T> where T : new()
    {
        public BinaryStorage()
        {
            FileSuffix = ".dat";
            DirectoryName = "Cache";
            DirectoryPath = Path.Combine(DirectoryPath, DirectoryName);
            FilePath = Path.Combine(DirectoryPath, FileName + FileSuffix);

            ValidateDirectory();
        }

        public override T Load()
        {
            if (File.Exists(FilePath))
            {
                using (var stream = new FileStream(FilePath, FileMode.Open))
                {
                    if (stream.Length > 0)
                    {
                        Deserialize(stream);
                    }
                    else
                    {
                        LoadDefault();
                    }
                }
            }
            else
            {
                LoadDefault();
            }
            return Data;
        }

        private void Deserialize(FileStream stream)
        {
            //http://stackoverflow.com/questions/2120055/binaryformatter-deserialize-gives-serializationexception
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            BinaryFormatter binaryFormatter = new BinaryFormatter
            {
                AssemblyFormat = FormatterAssemblyStyle.Simple
            };

            try
            {
                Data = (T)binaryFormatter.Deserialize(stream);
            }
            catch (SerializationException e)
            {
                Log.Error(e);
                LoadDefault();
            }
            catch (InvalidCastException e)
            {
                Log.Error(e);
                LoadDefault();
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            }
        }

        public override void LoadDefault()
        {
            Data = new T();
            Save();
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

        public override void Save()
        {
            using (var stream = new FileStream(FilePath, FileMode.Create))
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter
                {
                    AssemblyFormat = FormatterAssemblyStyle.Simple
                };

                try
                {
                    binaryFormatter.Serialize(stream, Data);
                }
                catch (SerializationException e)
                {
                    Log.Error(e);
                }
            }
        }
    }
}
