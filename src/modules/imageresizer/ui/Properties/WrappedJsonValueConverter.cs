﻿#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImageResizer.Properties
{
    public class WrappedJsonValueConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return true;
        }

        public override JsonConverter CreateConverter(
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (typeToConvert == null)
            {
                return null;
            }

            Type keyType = typeToConvert.UnderlyingSystemType;

            JsonConverter converter = (JsonConverter)Activator.CreateInstance(
                typeof(WrappedJsonConverter<>).MakeGenericType(keyType));

            return converter;
        }
    }
}
