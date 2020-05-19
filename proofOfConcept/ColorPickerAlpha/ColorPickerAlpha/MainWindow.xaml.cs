using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorPickerAlpha
{
    public partial class MainWindow : Window
    {
        private bool isClosing = false;
        bool rgbState = true;
        Color curColor;
        OverlayWindow overlayWnd;
        private bool _pickerActive = true;

        private int paletteIndex = 0;
        private int numPalette;
        private UIElement[] buttonArray;

        public bool pickerActive
        {
            get { return _pickerActive; }
            set 
            {
                _pickerActive = value;
                overlayWnd.Visibility = value ? Visibility.Visible : Visibility.Hidden;
                Topmost = false;
                Topmost = true;
            }
        }
        public bool isInWindow = false;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += delegate
            {
                MouseLeave += delegate { isInWindow = false; };
                MouseEnter += delegate { isInWindow = true; };
                Activated += delegate 
                { 
                    Topmost = true;
                    Mouse.OverrideCursor = null;
                };
                Deactivated += delegate 
                { 
                    Topmost = true;
                    Mouse.OverrideCursor = Cursors.Cross;
                };

                overlayWnd = new OverlayWindow(this);
                //both windows should be topmost, but the MainWindow above the overlay
                //=> both receive mouse input when needed. Owners are below children
                overlayWnd.Activated += delegate { Owner = overlayWnd; };
                overlayWnd.Show();

                numPalette = PaletteGrid.ColumnDefinitions.Count;
                GeneratePaletteHistory(numPalette);

                buttonArray = new UIElement[numPalette];
                PaletteGrid.Children.CopyTo(buttonArray, 0);
            };

            Closed += delegate 
            {
                Mouse.OverrideCursor = null;
                isClosing = true;
                overlayWnd.Close();
            };

            new Thread(() =>
            {
                while (!isClosing)
                {
                    if (!pickerActive || isInWindow)
                        continue;

                    (int x, int y) = ColorPicker.GetPhysicalCursorCoords();

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        System.Drawing.Color color = ColorPicker.GetPixelColor(x, y);

                        curColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                        Color_Box.Fill = new SolidColorBrush(curColor);
                        ChangeColorText();

                    }));

                    Thread.Sleep(100);
                }
            }).Start();
        }

        private void ChangeColorText()  
        {
            R_val.Text = curColor.R.ToString();
            G_val.Text = curColor.G.ToString();
            B_val.Text = curColor.B.ToString();

            HEXValue.Text = argbToHEX(curColor.ToString());
        }


        private void Toggle_RGB(object sender, RoutedEventArgs e)
        {
            rgbState = !rgbState;

            var rgbVisibility = rgbState ? Visibility.Visible : Visibility.Hidden;
            var hexVisibility = !rgbState ? Visibility.Visible : Visibility.Hidden;

            R_val.Visibility = rgbVisibility;
            G_val.Visibility = rgbVisibility;
            B_val.Visibility = rgbVisibility;

            RLabel.Visibility = rgbVisibility;
            GLabel.Visibility = rgbVisibility;
            BLabel.Visibility = rgbVisibility;

            HEXValue.Visibility = hexVisibility;
            HEXLabel.Visibility = hexVisibility;

        }

        private void Copy_Clip(object sender, RoutedEventArgs e) => CopyToClipboard(); 

        public void CopyToClipboard()
        {
            string argb = curColor.ToString();

            if (rgbState)
            {
                string rgbText = "(" + R_val.Text + ", " + G_val.Text + ", " + B_val.Text + ")";
                Clipboard.SetText(rgbText);
            }
            else
            {
                Clipboard.SetText(argbToHEX(argb));
            }
        }

        private string argbToHEX(string argb)
        {
            // RGB and ARGB formats
            StringBuilder hex = new StringBuilder();
            // Append the # sign in hex and remove the Alpha values from the ARGB format i.e) #AARRGGBB.
            hex.Append(argb[0]);
            hex.Append(argb.Substring(3));

            return hex.ToString();
        }

        private void Eyedropper_Click(object sender, RoutedEventArgs e)
        {
            pickerActive = !pickerActive;
        }


        private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e) => Close();
        

        // Generate the color palette buttons
        public void GeneratePaletteHistory(int columns)
        {
            for(int col = 0; col < columns; col++)
            {
                Button prevColorButton = new Button();
                prevColorButton.Background = new SolidColorBrush(Colors.Gray);
                prevColorButton.Margin = new Thickness(0, 0, 0, 0);


                PaletteGrid.Children.Add(prevColorButton);
                Grid.SetRow(prevColorButton, 1);
                Grid.SetColumn(prevColorButton, col);
            }
        }

        private void Palette_Button_Click(object sender, RoutedEventArgs e)
        {
            var source = e.OriginalSource as Button;

            if (source == null || !(source.Background is SolidColorBrush))
                return;

            var brush = (SolidColorBrush)source.Background;

            //update the main color box and copy value
            curColor = brush.Color;
            Color_Box.Fill = new SolidColorBrush(curColor);
            ChangeColorText();
            CopyToClipboard();
        }

        private void Save_To_Palette(object sender, RoutedEventArgs e)
        {

            var curButton = buttonArray[paletteIndex] as Button;
            if (curButton != null)
            {
                curButton.Background = new SolidColorBrush(curColor);
            }

            paletteIndex++;
            paletteIndex %= numPalette;
        }
    }
}
