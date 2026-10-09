// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <history>
//     2020-... created by Filip Jeremic (fjeremic) as "HexView.Wpf".
//     2024-... republished by @hotkidfamily as "HexBox.WinUI".
//     2025 Included in PowerToys. (Branch master; commit 72dcf64dc858c693a7a16887004c8ddbab61fce7.)
// </history>
#pragma warning disable SA1210 // Using directives should be ordered alphabetically by namespace
#pragma warning disable SA1208 // System using directives should be placed before other using directives
using RegistryPreviewUILib.HexBox.Library.EndianConvert;
using Microsoft.UI.Xaml.Data;
using System;
#pragma warning restore SA1208 // System using directives should be placed before other using directives
#pragma warning restore SA1210 // Using directives should be ordered alphabetically by namespace

namespace RegistryPreviewUILib.HexBox
{
    public partial class HexboxDataTypeConverter : IValueConverter
    {
        /// <summary>
        /// Convert a DataType value to its negation.
        /// </summary>
        /// <param name="value">The <see cref="DataType"/> value to negate.</param>
        /// <param name="targetType">The type of the target property, as a type reference.</param>
        /// <param name="parameter">Optional parameter. Not used.</param>
        /// <param name="language">The language of the conversion. Not used</param>
        /// <returns>The value to be passed to the target dependency property.</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DataType b)
            {
                if (parameter is string c)
                {
                    return c == b.ToString();
                }
            }
            throw new NotImplementedException();
        }

        /// <summary>
        /// Convert back a DataType value to its negation.
        /// </summary>
        /// <param name="value">The <see cref="DataType"/> value to negate.</param>
        /// <param name="targetType">The type of the target property, as a type reference.</param>
        /// <param name="parameter">Optional parameter. Not used.</param>
        /// <param name="language">The language of the conversion. Not used</param>
        /// <returns>The value to be passed to the target dependency property.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b && parameter is string c)
            {
                if(c == "Int_1")
                {
                    return DataType.Int_1;
                }
                else if (c == "Int_2")
                {
                    return DataType.Int_2;
                }
                else if (c == "Int_4")
                {
                    return DataType.Int_4;
                }
                else if (c == "Int_8")
                {
                    return DataType.Int_8;
                }
                else if (c == "Float_32")
                {
                    return DataType.Float_32;
                }
                else /*if (c == "Float_64")*/
                {
                    return DataType.Float_64;
                }
            }
            throw new NotImplementedException();
        }
    }

    public class HexboxDataSignednessConverter : IValueConverter
    {
        /// <summary>
        /// Convert a DataSignedness value to its negation.
        /// </summary>
        /// <param name="value">The <see cref="DataSignedness"/> value to negate.</param>
        /// <param name="targetType">The type of the target property, as a type reference.</param>
        /// <param name="parameter">Optional parameter. Not used.</param>
        /// <param name="language">The language of the conversion. Not used</param>
        /// <returns>The value to be passed to the target dependency property.</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DataSignedness b)
            {
                if (parameter is string c)
                {
                    var end = c == "Signed" ? DataSignedness.Signed : DataSignedness.Unsigned;
                    return (b == end);
                }
            }
            throw new NotImplementedException();
        }

        /// <summary>
        /// Convert back a DataSignedness value to its negation.
        /// </summary>
        /// <param name="value">The <see cref="DataSignedness"/> value to negate.</param>
        /// <param name="targetType">The type of the target property, as a type reference.</param>
        /// <param name="parameter">Optional parameter. Not used.</param>
        /// <param name="language">The language of the conversion. Not used</param>
        /// <returns>The value to be passed to the target dependency property.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b && parameter is string c)
            {
                var end = c == "Signed" ? DataSignedness.Signed : DataSignedness.Unsigned;
                if (b)
                {
                    return end;
                }
                else
                {
                    return c == "Signed" ? DataSignedness.Unsigned : DataSignedness.Signed;
                }
            }
            throw new NotImplementedException();
        }
    }

    public class HexboxDataFormatBoolConverter : IValueConverter
    {
        /// <summary>
        /// Convert a DataFormat value to its negation.
        /// </summary>
        /// <param name="value">The <see cref="DataFormat"/> value to negate.</param>
        /// <param name="targetType">The type of the target property, as a type reference.</param>
        /// <param name="parameter">Optional parameter. Not used.</param>
        /// <param name="language">The language of the conversion. Not used</param>
        /// <returns>The value to be passed to the target dependency property.</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if(value is DataFormat f)
            {
                return f != DataFormat.Hexadecimal;
            }
            throw new NotImplementedException();
        }

        /// <summary>
        /// Convert back a DataFormat value to its negation.
        /// </summary>
        /// <param name="value">The <see cref="DataFormat"/> value to negate.</param>
        /// <param name="targetType">The type of the target property, as a type reference.</param>
        /// <param name="parameter">Optional parameter. Not used.</param>
        /// <param name="language">The language of the conversion. Not used</param>
        /// <returns>The value to be passed to the target dependency property.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }


    public class HexboxDataFormatConverter : IValueConverter
    {
        /// <summary>
        /// Convert a DataFormat value to its negation.
        /// </summary>
        /// <param name="value">The <see cref="DataFormat"/> value to negate.</param>
        /// <param name="targetType">The type of the target property, as a type reference.</param>
        /// <param name="parameter">Optional parameter. Not used.</param>
        /// <param name="language">The language of the conversion. Not used</param>
        /// <returns>The value to be passed to the target dependency property.</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DataFormat b)
            {
                if (parameter is string c)
                {
                    var end = c == "Decimal" ? DataFormat.Decimal: DataFormat.Hexadecimal;
                    return (b == end);
                }
            }
            throw new NotImplementedException();
        }

        /// <summary>
        /// Convert back a DataFormat value to its negation.
        /// </summary>
        /// <param name="value">The <see cref="DataFormat"/> value to negate.</param>
        /// <param name="targetType">The type of the target property, as a type reference.</param>
        /// <param name="parameter">Optional parameter. Not used.</param>
        /// <param name="language">The language of the conversion. Not used</param>
        /// <returns>The value to be passed to the target dependency property.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b && parameter is string c)
            {
                var end = c == "Decimal" ? DataFormat.Decimal : DataFormat.Hexadecimal;
                if (b)
                {
                    return end;
                }
                else
                {
                    return end == DataFormat.Decimal ? DataFormat.Hexadecimal : DataFormat.Decimal;
                }
            }
            throw new NotImplementedException();
        }
    }


    public class BigEndianConverter : IValueConverter
    {
        /// <summary>
        /// Convert a Endian value to its negation.
        /// </summary>
        /// <param name="value">The <see cref="Endian"/> value to negate.</param>
        /// <param name="targetType">The type of the target property, as a type reference.</param>
        /// <param name="parameter">Optional parameter. Not used.</param>
        /// <param name="language">The language of the conversion. Not used</param>
        /// <returns>The value to be passed to the target dependency property.</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is Endianness b)
            {
                if (parameter is string c)
                {
                    var end = c == "BigEndian" ? Endianness.BigEndian : Endianness.LittleEndian;
                    return (b == end);
                }
            }
            throw new NotImplementedException();
        }

        /// <summary>
        /// Convert back a Endian value to its negation.
        /// </summary>
        /// <param name="value">The <see cref="Endian"/> value to negate.</param>
        /// <param name="targetType">The type of the target property, as a type reference.</param>
        /// <param name="parameter">Optional parameter. Not used.</param>
        /// <param name="language">The language of the conversion. Not used</param>
        /// <returns>The value to be passed to the target dependency property.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b && parameter is string c)
            {
                var end = c == "BigEndian" ? Endianness.BigEndian : Endianness.LittleEndian;
                if (b)
                {
                    return end;
                }
                else
                {
                    return end == Endianness.BigEndian ? Endianness.LittleEndian : Endianness.BigEndian;
                }
            }
            throw new NotImplementedException();
        }
    }
}
