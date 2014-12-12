using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Wox.Plugin.FindFile
{
    public partial class Setting : UserControl
    {
        public Setting()
        {
            InitializeComponent();
            menuGrid.ItemsSource = FindFileContextMenuStorage.Instance.ContextMenus;
            menuGrid.CurrentCellChanged += menuGrid_CurrentCellChanged;
        }

        void menuGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            FindFileContextMenuStorage.Instance.Save();
        }
    }
}
