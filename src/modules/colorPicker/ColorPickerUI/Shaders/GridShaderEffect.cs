// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ColorPicker.Shaders
{
    public class GridShaderEffect : ShaderEffect
    {
        private static readonly PixelShader Shader =
            new PixelShader()
            {
                UriSource = Global.MakePackUri("GridShader.cso"),
            };

        public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(GridShaderEffect), 0);
        public static readonly DependencyProperty MousePositionProperty = DependencyProperty.Register("MousePosition", typeof(Point), typeof(GridShaderEffect), new UIPropertyMetadata(new Point(0D, 0D), PixelShaderConstantCallback(1)));
        public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register("Radius", typeof(double), typeof(GridShaderEffect), new UIPropertyMetadata((double)0D, PixelShaderConstantCallback(2)));
        public static readonly DependencyProperty SquareSizeProperty = DependencyProperty.Register("SquareSize", typeof(double), typeof(GridShaderEffect), new UIPropertyMetadata((double)0D, PixelShaderConstantCallback(3)));
        public static readonly DependencyProperty TextureSizeProperty = DependencyProperty.Register("TextureSize", typeof(double), typeof(GridShaderEffect), new UIPropertyMetadata((double)0D, PixelShaderConstantCallback(4)));

        public GridShaderEffect()
        {
            PixelShader = Shader;

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(MousePositionProperty);
            UpdateShaderValue(RadiusProperty);
            UpdateShaderValue(SquareSizeProperty);
            UpdateShaderValue(TextureSizeProperty);
        }

        public Brush Input
        {
            get
            {
                return (Brush)GetValue(InputProperty);
            }

            set
            {
                SetValue(InputProperty, value);
            }
        }

        public Point MousePosition
        {
            get
            {
                return (Point)GetValue(MousePositionProperty);
            }

            set
            {
                SetValue(MousePositionProperty, value);
            }
        }

        public double Radius
        {
            get
            {
                return (double)GetValue(RadiusProperty);
            }

            set
            {
                SetValue(RadiusProperty, value);
            }
        }

        public double SquareSize
        {
            get
            {
                return (double)GetValue(SquareSizeProperty);
            }

            set
            {
                SetValue(SquareSizeProperty, value);
            }
        }

        public double TextureSize
        {
            get
            {
                return (double)GetValue(TextureSizeProperty);
            }

            set
            {
                SetValue(TextureSizeProperty, value);
            }
        }
    }
}
