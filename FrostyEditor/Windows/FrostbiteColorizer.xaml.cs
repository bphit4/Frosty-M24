using Frosty.Controls;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace FrostyEditor.Windows
{
    /// <summary>
    /// Interaction logic for FrostbiteColorizer.xaml
    /// </summary>
    public partial class FrostbiteColorizer : FrostyDockableWindow
    {
        public FrostbiteColorizer()
        {
            InitializeComponent();
        }

        private async void btnConvert_Click(object sender, RoutedEventArgs e)
        {
            btnConvert.Content = "Converted!";

            double num = Convert.ToInt32(redValue.Text);
            double num2 = Convert.ToInt32(greenValue.Text);
            double num3 = Convert.ToInt32(blueValue.Text);
            double num4 = num / 255.0;
            double num5 = num2 / 255.0;
            double num6 = num3 / 255.0;
            if (num4 <= 0.0404482362771082)
            {
                double num7 = Math.Round(num4 / 12.92, 7);
                xValue.Content = Convert.ToString(num7);
            }
            else
            {
                double num8 = Math.Round(Math.Pow((num4 + 0.055) / 1.055, 2.4), 7);
                xValue.Content = Convert.ToString(num8);
            }
            if (num5 <= 0.0404482362771082)
            {
                double num9 = Math.Round(num5 / 12.92, 7);
                yValue.Content = Convert.ToString(num9);
            }
            else
            {
                double num10 = Math.Round(Math.Pow((num5 + 0.055) / 1.055, 2.4), 7);
                yValue.Content = Convert.ToString(num10);
            }
            if (num6 <= 0.0404482362771082)
            {
                double num11 = Math.Round(num6 / 12.92, 7);
                zValue.Content = Convert.ToString(num11);
            }
            else
            {
                double num12 = Math.Round(Math.Pow((num6 + 0.055) / 1.055, 2.4), 7);
                zValue.Content = Convert.ToString(num12);
            }

            await Task.Delay(TimeSpan.FromSeconds(1));

            btnConvert.Content = "Convert";
        }

        private async void btnCopyX_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(xValue.Content.ToString());

            btnCopyX.Content = "Copied!";

            await Task.Delay(TimeSpan.FromSeconds(1));

            btnCopyX.Content = "Copy";
        }

        private async void btnCopyY_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(yValue.Content.ToString());

            btnCopyY.Content = "Copied!";

            await Task.Delay(TimeSpan.FromSeconds(1));

            btnCopyY.Content = "Copy";
        }

        private async void btnCopyZ_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(zValue.Content.ToString());

            btnCopyZ.Content = "Copied!";

            await Task.Delay(TimeSpan.FromSeconds(1));

            btnCopyZ.Content = "Copy";
        }

        private void rSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (colorOutput != null)
            {
                SolidColorBrush color = new SolidColorBrush(Color.FromRgb((byte)rSlider.Value, (byte)gSlider.Value, (byte)bSlider.Value));
                colorOutput.Background = color; 
            }
        }

        private void gSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (colorOutput != null)
            {
                SolidColorBrush color = new SolidColorBrush(Color.FromRgb((byte)rSlider.Value, (byte)gSlider.Value, (byte)bSlider.Value));
                colorOutput.Background = color;

            }
        }
        private void bSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (colorOutput != null)
            {
                SolidColorBrush color = new SolidColorBrush(Color.FromRgb((byte)rSlider.Value, (byte)gSlider.Value, (byte)bSlider.Value));
                colorOutput.Background = color;
            }
        }
    }
}
