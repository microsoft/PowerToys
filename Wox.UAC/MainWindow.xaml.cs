using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Wox.UAC
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            string[] param = Environment.GetCommandLineArgs();
            if (param.Length > 2)
            {
                switch (param[1])
                {
                    case "UAC":
                        Invoke(param[2], param[3], param[4]);
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
