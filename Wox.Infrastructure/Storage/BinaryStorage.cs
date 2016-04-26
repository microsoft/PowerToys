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
    public class BinaryStorage<T> where T : class, new()
    {
        private T _binary;

        private string FilePath { get; }
        private string FileName { get; }
        private const string FileSuffix = ".dat";
        private string DirectoryPath { get; }
        private const string DirectoryName = "Config";

        public BinaryStorage()
        {
            FileName = typeof(T).Name;
            DirectoryPath = Path.Combine(WoxDirectroy.Executable, DirectoryName);
            FilePath = Path.Combine(DirectoryPath, FileName + FileSuffix); ;
        }

        public T Load()
        {
            if (!Directory.Exists(DirectoryPath))
            {
                Directory.CreateDirectory(DirectoryPath);
            }

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
            return _binary;
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
                _binary = (T)binaryFormatter.Deserialize(stream);
            }
            catch (SerializationException e)
            {
                LoadDefault();
                Log.Error(e);
            }
            catch (InvalidCastException e)
            {
                LoadDefault();
                Log.Error(e);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            }
        }

        private void LoadDefault()
        {
            _binary = new T();
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

        public void Save()
        {
            using (var stream = new FileStream(FilePath, FileMode.Create))
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter
                {
                    AssemblyFormat = FormatterAssemblyStyle.Simple
                };

                try
                {
                    binaryFormatter.Serialize(stream, _binary);
                }
                catch (SerializationException e)
                {
                    Log.Error(e);
                }
            }
        }
    }
}
