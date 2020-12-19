using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PixivWPF.Common
{
    /// <summary>
    /// PixivItemTile.xaml 的交互逻辑
    /// </summary>
    public partial class PixivItemTile : UserControl
    {
        [Localizability(LocalizationCategory.None, Readability = Readability.Readable)]
        [TypeConverter(typeof(LengthConverter))]
        [Description("Size"), Category("Layout")]
        public double Size
        {
            get { return ((double)GetValue(SizeProperty)); }
            set
            {
                SetCurrentValue(SizeProperty, value);
                SetCurrentValue(WidthProperty, value);
            }
        }
        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            "Size", typeof( double ), typeof( ProgressRingCloud ), new PropertyMetadata( 128.0 )
        );


        public PixivItemTile()
        {
            InitializeComponent();
        }

        private void TileImage_TargetUpdated(object sender, DataTransferEventArgs e)
        {

        }

        private void TileBadge_TargetUpdated(object sender, DataTransferEventArgs e)
        {

        }
    }
}
