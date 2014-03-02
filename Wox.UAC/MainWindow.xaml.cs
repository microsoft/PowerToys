using System;
using System.Reflection;
using System.Windows;

namespace Wox.UAC
{
    public partial class MainWindow : Window
    {
        PluginInstaller installer = new PluginInstaller();

        public MainWindow()
        {
            InitializeComponent();
            string[] param = Environment.GetCommandLineArgs();
            if (param.Length > 1)
            {
                switch (param[1])
                {
                    case "UAC":
                        Invoke(param[2], param[3], param[4]);
                        break;

                    case "AssociatePluginInstaller":
                        installer.RegisterInstaller();
                        break;

                    case "InstallPlugin":
                        var path = param[2];
                        installer.Install(path);
                        break;
                }
            }
            Application.Current.Shutdown(0);
        }

        private static void Invoke(string namespaceName, string className, string methodName)
        {
            Type type = Type.GetType(namespaceName + "." + className + "," + namespaceName);
            if (type != null)
            {
                object instance = Activator.CreateInstance(type);
                MethodInfo method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null) method.Invoke(instance, null);
            }
        }
    }
}
