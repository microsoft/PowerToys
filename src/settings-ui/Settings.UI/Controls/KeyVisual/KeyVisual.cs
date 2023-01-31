// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Windows.System;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    [TemplatePart(Name = KeyPresenter, Type = typeof(ContentPresenter))]
    [TemplateVisualState(Name = "Normal", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Disabled", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Default", GroupName = "StateStates")]
    [TemplateVisualState(Name = "Error", GroupName = "StateStates")]
    public sealed class KeyVisual : Control
    {
        private const string KeyPresenter = "KeyPresenter";
        private KeyVisual _keyVisual;
        private ContentPresenter _keyPresenter;

        public object Content
        {
            get => (object)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register("Content", typeof(object), typeof(KeyVisual), new PropertyMetadata(default(string), OnContentChanged));

        public VisualType VisualType
        {
            get => (VisualType)GetValue(VisualTypeProperty);
            set => SetValue(VisualTypeProperty, value);
        }

        public static readonly DependencyProperty VisualTypeProperty = DependencyProperty.Register("VisualType", typeof(VisualType), typeof(KeyVisual), new PropertyMetadata(default(VisualType), OnSizeChanged));

        public bool IsError
        {
            get => (bool)GetValue(IsErrorProperty);
            set => SetValue(IsErrorProperty, value);
        }

        public static readonly DependencyProperty IsErrorProperty = DependencyProperty.Register("IsError", typeof(bool), typeof(KeyVisual), new PropertyMetadata(false, OnIsErrorChanged));

        public KeyVisual()
        {
            this.DefaultStyleKey = typeof(KeyVisual);
            this.Style = GetStyleSize("TextKeyVisualStyle");
        }

        protected override void OnApplyTemplate()
        {
            IsEnabledChanged -= KeyVisual_IsEnabledChanged;
            _keyVisual = (KeyVisual)this;
            _keyPresenter = (ContentPresenter)_keyVisual.GetTemplateChild(KeyPresenter);
            Update();
            SetEnabledState();
            SetErrorState();
            IsEnabledChanged += KeyVisual_IsEnabledChanged;
            base.OnApplyTemplate();
        }

        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KeyVisual)d).Update();
        }

        private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KeyVisual)d).Update();
        }

        private static void OnIsErrorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KeyVisual)d).SetErrorState();
        }

        private void Update()
        {
            if (_keyVisual == null)
            {
                return;
            }

            if (_keyVisual.Content != null)
            {
                if (_keyVisual.Content.GetType() == typeof(string))
                {
                    string currentLanguage = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
                    currentLanguage = "fr";

                    _keyVisual.Style = GetStyleSize("TextKeyVisualStyle");
                    _keyVisual._keyPresenter.Content = _keyVisual.Content;

                    if (currentLanguage == "de")
                    {
                        if ((string)_keyVisual._keyPresenter.Content == "Ctrl")
                        {
                            _keyVisual._keyPresenter.Content = "Strg";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Shift")
                        {
                            _keyVisual._keyPresenter.Content = "Umschalt";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "PgUp")
                        {
                            _keyVisual._keyPresenter.Content = "Bild auf";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "PgDn")
                        {
                            _keyVisual._keyPresenter.Content = "Bild ab";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Insert")
                        {
                            _keyVisual._keyPresenter.Content = "Einfg";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Delete")
                        {
                            _keyVisual._keyPresenter.Content = "Entf";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Home")
                        {
                            _keyVisual._keyPresenter.Content = "Pos1";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "End")
                        {
                            _keyVisual._keyPresenter.Content = "Ende";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Print Screen")
                        {
                            _keyVisual._keyPresenter.Content = "Druck";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Space")
                        {
                            _keyVisual._keyPresenter.Content = "Leertaste";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Enter")
                        {
                            _keyVisual._keyPresenter.Content = "Eingabetaste";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Backspace")
                        {
                            _keyVisual._keyPresenter.Content = "Rücktaste";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Caps Lock")
                        {
                            _keyVisual._keyPresenter.Content = "Feststelltaste";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Scroll Lock")
                        {
                            _keyVisual._keyPresenter.Content = "Rollen";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Apps/Menu")
                        {
                            _keyVisual._keyPresenter.Content = "Menü";
                        }
                    }
                    else if (currentLanguage == "fr")
                    {
                        if ((string)_keyVisual._keyPresenter.Content == "Shift")
                        {
                            _keyVisual._keyPresenter.Content = "Maj";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "PgUp")
                        {
                            _keyVisual._keyPresenter.Content = "Page précédente";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "PgDn")
                        {
                            _keyVisual._keyPresenter.Content = "Page suivante";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Tab")
                        {
                            _keyVisual._keyPresenter.Content = "Tabulation";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Insert")
                        {
                            _keyVisual._keyPresenter.Content = "Insertion";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Delete")
                        {
                            _keyVisual._keyPresenter.Content = "Supprimer";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Home")
                        {
                            _keyVisual._keyPresenter.Content = "Accueil";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "End")
                        {
                            _keyVisual._keyPresenter.Content = "Fin";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Print Screen")
                        {
                            _keyVisual._keyPresenter.Content = "Imp. écr.";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Space")
                        {
                            _keyVisual._keyPresenter.Content = "Barre d’espace";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Enter")
                        {
                            _keyVisual._keyPresenter.Content = "Entrée";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Backspace")
                        {
                            _keyVisual._keyPresenter.Content = "Retour arrière";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Caps Lock")
                        {
                            _keyVisual._keyPresenter.Content = "Verr Maj";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Scroll Lock")
                        {
                            _keyVisual._keyPresenter.Content = "Arrêt défil";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Num Lock")
                        {
                            _keyVisual._keyPresenter.Content = "Ver. num.";
                        }
                    }
                    else if (currentLanguage == "es")
                    {
                        if ((string)_keyVisual._keyPresenter.Content == "Shift")
                        {
                            _keyVisual._keyPresenter.Content = "Mayús";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "PgUp")
                        {
                            _keyVisual._keyPresenter.Content = "Re Pág";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "PgDn")
                        {
                            _keyVisual._keyPresenter.Content = "Av Pág";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Tab")
                        {
                            _keyVisual._keyPresenter.Content = "Tabulador";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Insert")
                        {
                            _keyVisual._keyPresenter.Content = "Insertar";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Delete")
                        {
                            _keyVisual._keyPresenter.Content = "Supr";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Home")
                        {
                            _keyVisual._keyPresenter.Content = "Inicio";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "End")
                        {
                            _keyVisual._keyPresenter.Content = "Fin";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Space")
                        {
                            _keyVisual._keyPresenter.Content = "Barra espaciadora";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Enter")
                        {
                            _keyVisual._keyPresenter.Content = "Entrar";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Backspace")
                        {
                            _keyVisual._keyPresenter.Content = "Retroceso";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Caps Lock")
                        {
                            _keyVisual._keyPresenter.Content = "Bloq Mayús";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Scroll Lock")
                        {
                            _keyVisual._keyPresenter.Content = "Bloq Despl";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Num Lock")
                        {
                            _keyVisual._keyPresenter.Content = "Bloq Num";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Pause")
                        {
                            _keyVisual._keyPresenter.Content = "Pausa";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Apps/Menu")
                        {
                            _keyVisual._keyPresenter.Content = "Menú";
                        }
                    }
                    else if (currentLanguage == "it")
                    {
                        if ((string)_keyVisual._keyPresenter.Content == "Shift")
                        {
                            _keyVisual._keyPresenter.Content = "Maiusc";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "PgUp")
                        {
                            _keyVisual._keyPresenter.Content = "PGSU";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "PgDn")
                        {
                            _keyVisual._keyPresenter.Content = "PGGIÙ";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Insert")
                        {
                            _keyVisual._keyPresenter.Content = "Insertar";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Delete")
                        {
                            _keyVisual._keyPresenter.Content = "Canc";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "End")
                        {
                            _keyVisual._keyPresenter.Content = "Fine";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Space")
                        {
                            _keyVisual._keyPresenter.Content = "Barra spaziatrice";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Enter")
                        {
                            _keyVisual._keyPresenter.Content = "Invio";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Caps Lock")
                        {
                            _keyVisual._keyPresenter.Content = "Blocco maiuscole";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Scroll Lock")
                        {
                            _keyVisual._keyPresenter.Content = "Bloc Scorr";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Num Lock")
                        {
                            _keyVisual._keyPresenter.Content = "Bloc Num";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Pause")
                        {
                            _keyVisual._keyPresenter.Content = "Pausa";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Print Screen")
                        {
                            _keyVisual._keyPresenter.Content = "Stamp";
                        }
                    }
                    else if (currentLanguage == "pt")
                    {
                        if ((string)_keyVisual._keyPresenter.Content == "Tab")
                        {
                            _keyVisual._keyPresenter.Content = "Tabulação";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Space")
                        {
                            _keyVisual._keyPresenter.Content = "Barra de espaço";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Pause")
                        {
                            _keyVisual._keyPresenter.Content = "Pausa";
                        }
                    }
                    else
                    {
                        if ((string)_keyVisual._keyPresenter.Content == "PgUp")
                        {
                            _keyVisual._keyPresenter.Content = "Page up";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "PgDn")
                        {
                            _keyVisual._keyPresenter.Content = "Page down";
                        }
                        else if ((string)_keyVisual._keyPresenter.Content == "Apps/Menu")
                        {
                            _keyVisual._keyPresenter.Content = "Menu";
                        }
                    }
                }
                else
                {
                    _keyVisual.Style = GetStyleSize("IconKeyVisualStyle");

                    int test = (int)_keyVisual.Content;

                    switch (test)
                    {
                        /* We can enable other glyphs in the future
                        case 13: // The Enter key or button.
                            _keyVisual._keyPresenter.Content = "\uE751"; break;

                        case 8: // The Back key or button.
                            _keyVisual._keyPresenter.Content = "\uE750"; break;

                        case 16: // The right Shift key or button.
                        case 160: // The left Shift key or button.
                        case 161: // The Shift key or button.
                            _keyVisual._keyPresenter.Content = "\uE752"; break; */

                        case 38: _keyVisual._keyPresenter.Content = "\uE0E4"; break; // The Up Arrow key or button.
                        case 40: _keyVisual._keyPresenter.Content = "\uE0E5"; break; // The Down Arrow key or button.
                        case 37: _keyVisual._keyPresenter.Content = "\uE0E2"; break; // The Left Arrow key or button.
                        case 39: _keyVisual._keyPresenter.Content = "\uE0E3"; break; // The Right Arrow key or button.

                        case 91: // The left Windows key
                        case 92: // The right Windows key
                            PathIcon winIcon = XamlReader.Load(@"<PathIcon xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Data=""M9,17V9h8v8ZM0,17V9H8v8ZM9,8V0h8V8ZM0,8V0H8V8Z"" />") as PathIcon;
                            Viewbox winIconContainer = new Viewbox();
                            winIconContainer.Child = winIcon;
                            winIconContainer.HorizontalAlignment = HorizontalAlignment.Center;
                            winIconContainer.VerticalAlignment = VerticalAlignment.Center;

                            double iconDimensions = GetIconSize();
                            winIconContainer.Height = iconDimensions;
                            winIconContainer.Width = iconDimensions;
                            _keyVisual._keyPresenter.Content = winIconContainer;
                            break;
                        default: _keyVisual._keyPresenter.Content = ((VirtualKey)_keyVisual.Content).ToString(); break;
                    }
                }
            }
        }

        public Style GetStyleSize(string styleName)
        {
            if (VisualType == VisualType.Small)
            {
                return (Style)App.Current.Resources["Small" + styleName];
            }
            else if (VisualType == VisualType.SmallOutline)
            {
                return (Style)App.Current.Resources["SmallOutline" + styleName];
            }
            else
            {
                return (Style)App.Current.Resources["Default" + styleName];
            }
        }

        public double GetIconSize()
        {
            if (VisualType == VisualType.Small || VisualType == VisualType.SmallOutline)
            {
                return (double)App.Current.Resources["SmallIconSize"];
            }
            else
            {
                return (double)App.Current.Resources["DefaultIconSize"];
            }
        }

        private void KeyVisual_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetEnabledState();
        }

        private void SetErrorState()
        {
            VisualStateManager.GoToState(this, IsError ? "Error" : "Default", true);
        }

        private void SetEnabledState()
        {
            VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
        }
    }

    public enum VisualType
    {
        Small,
        SmallOutline,
        Large,
    }
}
