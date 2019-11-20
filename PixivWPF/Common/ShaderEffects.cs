using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace PixivWPF.Common
{
    public class ThresholdEffect : ShaderEffect
    {
        private static PixelShader _pixelShader =
            new PixelShader() { UriSource = MakePackUri("Resources/ThresholdEffect.fx.ps") };

        public ThresholdEffect()
        {
            PixelShader = _pixelShader;

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(ThresholdProperty);
            UpdateShaderValue(BlankColorProperty);
        }

        // MakePackUri is a utility method for computing a pack uri
        // for the given resource. 
        public static Uri MakePackUri(string relativeFile)
        {
            Assembly a = typeof(ThresholdEffect).Assembly;

            // Extract the short name.
            string assemblyShortName = a.ToString().Split(',')[0];

            string uriString = "pack://application:,,,/" +
                assemblyShortName +
                ";component/" +
                relativeFile;

            return new Uri(uriString);
        }

        ///////////////////////////////////////////////////////////////////////
        #region Input dependency property

        public Brush Input
        {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        public static readonly DependencyProperty InputProperty =
            ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(ThresholdEffect), 0);

        #endregion

        ///////////////////////////////////////////////////////////////////////
        #region Threshold dependency property

        public double Threshold
        {
            get { return (double)GetValue(ThresholdProperty); }
            set { SetValue(ThresholdProperty, value); }
        }

        public static readonly DependencyProperty ThresholdProperty =
            DependencyProperty.Register("Threshold", typeof(double), typeof(ThresholdEffect),
                    new UIPropertyMetadata(0.5, PixelShaderConstantCallback(0)));

        #endregion

        ///////////////////////////////////////////////////////////////////////
        #region BlankColor dependency property

        public Color BlankColor
        {
            get { return (Color)GetValue(BlankColorProperty); }
            set { SetValue(BlankColorProperty, value); }
        }

        public static readonly DependencyProperty BlankColorProperty =
            DependencyProperty.Register("BlankColor", typeof(Color), typeof(ThresholdEffect),
                    new UIPropertyMetadata(Colors.Transparent, PixelShaderConstantCallback(1)));

        #endregion
    }

    public class TransparenceEffect : ShaderEffect
    {
        private static PixelShader _pixelShader =
            new PixelShader() { UriSource = MakePackUri("Resources/TransparenceEffect.fx.ps") };

        public TransparenceEffect()
        {
            PixelShader = _pixelShader;

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(TransColorProperty);
        }

        // MakePackUri is a utility method for computing a pack uri
        // for the given resource. 
        public static Uri MakePackUri(string relativeFile)
        {
            Assembly a = typeof(TransparenceEffect).Assembly;

            // Extract the short name.
            string assemblyShortName = a.ToString().Split(',')[0];

            string uriString = "pack://application:,,,/" +
                assemblyShortName +
                ";component/" +
                relativeFile;

            return new Uri(uriString);
        }

        ///////////////////////////////////////////////////////////////////////
        #region Input dependency property

        public Brush Input
        {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        public static readonly DependencyProperty InputProperty =
            ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(TransparenceEffect), 0);

        #endregion

        ///////////////////////////////////////////////////////////////////////
        #region TransColor dependency property

        public Color TransColor
        {
            get { return (Color)GetValue(TransColorProperty); }
            set { SetValue(TransColorProperty, value); }
        }

        public static readonly DependencyProperty TransColorProperty =
            DependencyProperty.Register("TransColor", typeof(Color), typeof(TransparenceEffect),
                    new UIPropertyMetadata(Colors.Transparent, PixelShaderConstantCallback(0)));

        #endregion
    }

    public class ReplaceColorEffect : ShaderEffect
    {
        private static PixelShader _pixelShader =
            new PixelShader() { UriSource = MakePackUri("Resources/ReplaceColorEffect.fx.ps") };

        public ReplaceColorEffect()
        {
            PixelShader = _pixelShader;

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(ThresholdProperty);
            UpdateShaderValue(SourceColorProperty);
            UpdateShaderValue(TargetColorProperty);
        }

        // MakePackUri is a utility method for computing a pack uri
        // for the given resource. 
        public static Uri MakePackUri(string relativeFile)
        {
            Assembly a = typeof(ReplaceColorEffect).Assembly;

            // Extract the short name.
            string assemblyShortName = a.ToString().Split(',')[0];

            string uriString = "pack://application:,,,/" +
                assemblyShortName +
                ";component/" +
                relativeFile;

            return new Uri(uriString);
        }

        ///////////////////////////////////////////////////////////////////////
        #region Input dependency property

        public Brush Input
        {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        public static readonly DependencyProperty InputProperty =
            ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(ReplaceColorEffect), 0);

        #endregion

        ///////////////////////////////////////////////////////////////////////
        #region Threshold dependency property

        public double Threshold
        {
            get { return (double)GetValue(ThresholdProperty); }
            set { SetValue(ThresholdProperty, value); }
        }

        public static readonly DependencyProperty ThresholdProperty =
            DependencyProperty.Register("Threshold", typeof(double), typeof(ReplaceColorEffect),
                    new UIPropertyMetadata(0.5, PixelShaderConstantCallback(0)));

        #endregion

        ///////////////////////////////////////////////////////////////////////
        #region SourceColor dependency property

        public Color SourceColor
        {
            get { return (Color)GetValue(SourceColorProperty); }
            set { SetValue(SourceColorProperty, value); }
        }

        public static readonly DependencyProperty SourceColorProperty =
            DependencyProperty.Register("SourceColor", typeof(Color), typeof(ReplaceColorEffect),
                    new UIPropertyMetadata(Colors.Transparent, PixelShaderConstantCallback(1)));

        #endregion

        ///////////////////////////////////////////////////////////////////////
        #region TargetColor dependency property

        public Color TargetColor
        {
            get { return (Color)GetValue(TargetColorProperty); }
            set { SetValue(TargetColorProperty, value); }
        }

        public static readonly DependencyProperty TargetColorProperty =
            DependencyProperty.Register("TargetColor", typeof(Color), typeof(ReplaceColorEffect),
                    new UIPropertyMetadata(Colors.Transparent, PixelShaderConstantCallback(2)));

        #endregion        
    }

    public class ExcludeReplaceColorEffect : ShaderEffect
    {
        private static PixelShader _pixelShader =
            new PixelShader() { UriSource = MakePackUri("Resources/ExcludeReplaceColorEffect.fx.ps") };

        public ExcludeReplaceColorEffect()
        {
            PixelShader = _pixelShader;

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(ThresholdProperty);
            UpdateShaderValue(ExcludeColorProperty);
            UpdateShaderValue(TargetColorProperty);
        }

        // MakePackUri is a utility method for computing a pack uri
        // for the given resource. 
        public static Uri MakePackUri(string relativeFile)
        {
            Assembly a = typeof(ExcludeReplaceColorEffect).Assembly;

            // Extract the short name.
            string assemblyShortName = a.ToString().Split(',')[0];

            string uriString = "pack://application:,,,/" +
                assemblyShortName +
                ";component/" +
                relativeFile;

            return new Uri(uriString);
        }

        ///////////////////////////////////////////////////////////////////////
        #region Input dependency property

        public Brush Input
        {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        public static readonly DependencyProperty InputProperty =
            ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(ExcludeReplaceColorEffect), 0);

        #endregion

        ///////////////////////////////////////////////////////////////////////
        #region Threshold dependency property

        public double Threshold
        {
            get { return (double)GetValue(ThresholdProperty); }
            set { SetValue(ThresholdProperty, value); }
        }

        public static readonly DependencyProperty ThresholdProperty =
            DependencyProperty.Register("Threshold", typeof(double), typeof(ExcludeReplaceColorEffect),
                    new UIPropertyMetadata(0.5, PixelShaderConstantCallback(0)));

        #endregion

        ///////////////////////////////////////////////////////////////////////
        #region ExcludeColor dependency property

        public Color ExcludeColor
        {
            get { return (Color)GetValue(ExcludeColorProperty); }
            set { SetValue(ExcludeColorProperty, value); }
        }

        public static readonly DependencyProperty ExcludeColorProperty =
            DependencyProperty.Register("ExcludeColor", typeof(Color), typeof(ExcludeReplaceColorEffect),
                    new UIPropertyMetadata(Colors.Transparent, PixelShaderConstantCallback(1)));

        #endregion

        ///////////////////////////////////////////////////////////////////////
        #region TargetColor dependency property

        public Color TargetColor
        {
            get { return (Color)GetValue(TargetColorProperty); }
            set { SetValue(TargetColorProperty, value); }
        }

        public static readonly DependencyProperty TargetColorProperty =
            DependencyProperty.Register("TargetColor", typeof(Color), typeof(ExcludeReplaceColorEffect),
                    new UIPropertyMetadata(Colors.Transparent, PixelShaderConstantCallback(2)));

        #endregion        
    }
}
