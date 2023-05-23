// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <summary>
//     About box.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

namespace MouseWithoutBorders
{
    internal partial class FrmAbout : System.Windows.Forms.Form, IDisposable
    {
        internal FrmAbout()
        {
            InitializeComponent();
            Text = string.Format(CultureInfo.CurrentCulture, "About {0}", AssemblyTitle);
            labelProductName.Text = string.Format(CultureInfo.CurrentCulture, "{0} {1}", AssemblyProduct, AssemblyVersion);
            labelCopyright.Text = AssemblyCopyright;
            labelCompanyName.Text = "Project creator: Truong Do (Đỗ Đức Trường)";

            textBoxContributors.Text += "* Microsoft Garage: Quinn Hawkins, Michael Low, Joe Coplen, Nino Yuniardi, Gwyneth Marshall, David Andrews, Karen Luecking";
            textBoxContributors.Text += "\r\n* Peter Hauge\t\t- Visual Studio";
            textBoxContributors.Text += "\r\n* Bruce Dawson\t\t- Windows Fundamentals";
            textBoxContributors.Text += "\r\n* Alan Myrvold\t\t- Office Security";
            textBoxContributors.Text += "\r\n* Adrian Garside\t\t- WEX";
            textBoxContributors.Text += "\r\n* Scott Bradner\t\t- Surface";
            textBoxContributors.Text += "\r\n* Aleks Gershaft\t\t- Windows Azure";
            textBoxContributors.Text += "\r\n* Chinh Huynh\t\t- Windows Azure";
            textBoxContributors.Text += "\r\n* Long Nguyen\t\t- Data Center";
            textBoxContributors.Text += "\r\n* Triet Le\t\t\t- Cloud Engineering";
            textBoxContributors.Text += "\r\n* Luke Schoen\t\t- Excel";
            textBoxContributors.Text += "\r\n* Bao Nguyen\t\t- Bing";
            textBoxContributors.Text += "\r\n* Ross Nichols\t\t- Windows";
            textBoxContributors.Text += "\r\n* Ryan Baltazar\t\t- Windows";
            textBoxContributors.Text += "\r\n* Ed Essey\t\t- The Garage";
            textBoxContributors.Text += "\r\n* Mario Madden\t\t- The Garage";
            textBoxContributors.Text += "\r\n* Karthick Mahalingam\t- ACE";
            textBoxContributors.Text += "\r\n* Pooja Kamra\t\t- ACE";
            textBoxContributors.Text += "\r\n* Justin White\t\t- SA";
            textBoxContributors.Text += "\r\n* Chris Ransom\t\t- SA";
            textBoxContributors.Text += "\r\n* Mike Ricks\t\t- Red Team";
            textBoxContributors.Text += "\r\n* Randy Santossio\t\t- Surface";
            textBoxContributors.Text += "\r\n* Ashish Sen Jaswal\t\t- Device Health";
            textBoxContributors.Text += "\r\n* Zoltan Harmath\t\t- Security Tools";
            textBoxContributors.Text += "\r\n* Luciano Krigun\t\t- Security Products";
            textBoxContributors.Text += "\r\n* Jo Hemmerlein\t\t- Red Team";
            textBoxContributors.Text += "\r\n* Chris Johnson\t\t- Surface Hub";
            textBoxContributors.Text += "\r\n* Loren Ponten\t\t- Surface Hub";
            textBoxContributors.Text += "\r\n* Paul Schmitt\t\t- WWL";

            textBoxContributors.Text += "\r\n\r\n* And many other Users!";
        }

        internal static string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != null && titleAttribute.Title.Length > 0)
                    {
                        return titleAttribute.Title;
                    }
                }

                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
            }
        }

        internal static string AssemblyVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString();

        internal static string AssemblyDescription
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                return attributes.Length == 0 ? string.Empty : ((AssemblyDescriptionAttribute)attributes[0]).Description;
            }
        }

        internal static string AssemblyProduct
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                return attributes.Length == 0 ? string.Empty : ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        internal static string AssemblyCopyright
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                return attributes.Length == 0 ? string.Empty : ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        internal static string AssemblyCompany
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                return attributes.Length == 0 ? string.Empty : ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
        }

        private void FrmAbout_FormClosed(object sender, FormClosedEventArgs e)
        {
            Common.AboutForm = null;
        }
    }
}
