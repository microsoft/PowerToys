using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;

namespace Wox.Plugin.SystemPlugins
{
    public sealed class ColorsPlugin : BaseSystemPlugin
    {
        private const string DIR_PATH = ".\\Plugins\\Colors\\";
        private const int IMG_SIZE = 32;

        private DirectoryInfo ColorsDirectory { get; set; }

        public ColorsPlugin()
        {
            if (!Directory.Exists(DIR_PATH))
            {
                ColorsDirectory = Directory.CreateDirectory(DIR_PATH);
            }
            else
            {
                ColorsDirectory = new DirectoryInfo(DIR_PATH);
            }
        }

        protected override List<Result> QueryInternal(Query query)
        {
            var raw = query.RawQuery;
            if (!IsAvailable(raw)) return new List<Result>(0);
            try
            {
                var cached = Find(raw);
                if (cached.Length == 0)
                {
                    var path = CreateImage(raw);
                    return new List<Result>
                    {
                        new Result
                        {
                            Title = raw,
                            IcoPath = path,
                            Action = _ =>
                            {
                                Clipboard.SetText(raw);
                                return true;
                            }
                        }
                    };
                }
                return cached.Select(x => new Result
                {
                    Title = raw,
                    IcoPath = x.FullName,
                    Action = _ =>
                    {
                        Clipboard.SetText(raw);
                        return true;
                    }
                }).ToList();
            }
            catch (Exception exception)
            {
                // todo: log
                return new List<Result>(0);
            }
        }

        private bool IsAvailable(string query)
        {
            // todo: rgb, names
            var length = query.Length - 1; // minus `#` sign
            return query.StartsWith("#") && (length == 3 || length == 6);
        }

        public FileInfo[] Find(string name)
        {
            var file = string.Format("{0}.png", name.Substring(1));
            return ColorsDirectory.GetFiles(file, SearchOption.TopDirectoryOnly);
        }

        public string CreateImage(string name)
        {
            using (var bitmap = new Bitmap(IMG_SIZE, IMG_SIZE))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var color = ColorTranslator.FromHtml(name);
                graphics.Clear(color);

                var path = CreateFileName(name);
                bitmap.Save(path, ImageFormat.Png);
                return path;
            }
        }

        private string CreateFileName(string name)
        {
            return string.Format("{0}{1}.png", ColorsDirectory.FullName, name.Substring(1));
        }

        public override string Name
        {
            get { return "Colors"; }
        }

        public override string Description
        {
            get { return string.Empty; }
        }

        public override string IcoPath
        {
            get { return "Images/color2.png"; }
        }

        protected override void InitInternal(PluginInitContext context)
        {
        }
    }
}