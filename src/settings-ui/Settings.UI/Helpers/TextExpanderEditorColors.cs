// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Chooses the colors for the snippet editor's replacement box: ordinary text in the box's own
    /// text color, and variables in accent text on a faint tint.
    /// </summary>
    internal static class TextExpanderEditorColors
    {
        /// <summary>How much of the accent color tints a variable's background.</summary>
        internal const double VariableBackgroundTint = 0.14;

        /// <summary>
        /// Gets the color for text that isn't a variable: the text color XAML gave the box for its
        /// current theme and state, which it keeps as the document's default format.
        ///
        /// <para>
        /// Never RichEdit's "automatic" color. In the box that means the Win32 window text color,
        /// which is black even in dark mode. Typed text takes the color of the character before it,
        /// so once a variable is colored, the text after it needs an explicit color too.
        /// </para>
        /// </summary>
        /// <param name="documentDefault">The document's default text color.</param>
        /// <param name="automatic">RichEdit's automatic color, <c>TextConstants.AutoColor</c>.</param>
        /// <param name="undefined">RichEdit's undefined color, <c>TextConstants.UndefinedColor</c>.</param>
        /// <param name="controlForeground">The box's foreground, used if the default isn't a real color yet.</param>
        public static Color PlainText(Color documentDefault, Color automatic, Color undefined, Color controlForeground)
        {
            bool usable = documentDefault.A != 0 && !documentDefault.Equals(automatic) && !documentDefault.Equals(undefined);
            Color chosen = usable ? documentDefault : controlForeground;

            // RichEdit draws opaque colors; XAML applies the box's own opacity separately.
            return new Color { A = 255, R = chosen.R, G = chosen.G, B = chosen.B };
        }

        /// <summary>
        /// Gets the colors for variables: accent text on a faint accent tint, mixed over the box's
        /// background because RichEdit draws opaque colors. None in high contrast, where the system
        /// colors apply unchanged.
        /// </summary>
        /// <param name="highContrast">Whether high contrast is on.</param>
        /// <param name="accent">The theme's accent text color.</param>
        /// <param name="box">The box's background, which may be translucent.</param>
        /// <param name="page">The solid color behind the box.</param>
        public static (Color Text, Color Background)? Variables(bool highContrast, Color accent, Color box, Color page)
        {
            if (highContrast)
            {
                return null;
            }

            Color background = Blend(box, page, box.A / 255.0);
            Color text = Blend(accent, background, accent.A / 255.0);
            return (text, Blend(text, background, VariableBackgroundTint));
        }

        /// <summary>
        /// Gets a value indicating whether variables are underlined: whenever they get no colors
        /// of their own, as in high contrast, so they still stand out without color.
        /// </summary>
        /// <param name="colors">What <see cref="Variables"/> gave.</param>
        public static bool UnderlinesVariables((Color Text, Color Background)? colors) => colors is null;

        /// <summary>Mixes <paramref name="amount"/> of <paramref name="top"/> over <paramref name="bottom"/>, giving an opaque color.</summary>
        internal static Color Blend(Color top, Color bottom, double amount)
        {
            byte Mix(byte over, byte under) => (byte)Math.Round((over * amount) + (under * (1 - amount)));
            return new Color { A = 255, R = Mix(top.R, bottom.R), G = Mix(top.G, bottom.G), B = Mix(top.B, bottom.B) };
        }
    }
}
