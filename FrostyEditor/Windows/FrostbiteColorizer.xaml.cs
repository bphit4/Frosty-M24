using Frosty.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
    public enum MouseMessages
    {
        WM_LBUTTONDOWN = 0x0201,
        WM_LBUTTONUP = 0x0202,
        WM_MOUSEMOVE = 0x0200,
        WM_MOUSEWHEEL = 0x020A,
        WM_RBUTTONDOWN = 0x0204,
        WM_RBUTTONUP = 0x0205
    }

    public partial class FrostbiteColorizer : FrostyDockableWindow
    {
        private static FrostbiteColorizer _instance;

        // P/Invoke signatures
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        public static extern int BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true)]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        // Add this line for GetModuleHandle
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        // Fixed StructLayout position
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private bool isEyedropperActive = false;
        private static LowLevelMouseProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;
 
        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (System.Diagnostics.Process curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (System.Diagnostics.ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(14, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && MouseMessages.WM_LBUTTONDOWN == (MouseMessages)wParam)
            {
                if (GetCursorPos(out POINT point))
                {
                    Color color = GetPixelColor(point.X, point.Y);
                    _instance.Dispatcher.Invoke(() =>
                    {
                        _instance.Topmost = true;  // Make the window stay on top
                                                   // Update your UI here
                        _instance.rSlider.Value = color.R;
                        _instance.gSlider.Value = color.G;
                        _instance.bSlider.Value = color.B;

                        // Deactivate the eyedropper after picking a color
                        _instance.isEyedropperActive = false;
                        UnhookWindowsHookEx(_hookID);
                        _instance.Cursor = Cursors.Arrow;

                        _instance.Topmost = true;  // Make the window not stay on top anymore
                    });
                }
            }
            else if (nCode >= 0)
            {
                // Continue updating the color without deactivating the eyedropper
                if (GetCursorPos(out POINT point))
                {
                    Color color = GetPixelColor(point.X, point.Y);
                    _instance.Dispatcher.Invoke(() =>
                    {
                        _instance.rSlider.Value = color.R;
                        _instance.gSlider.Value = color.G;
                        _instance.bSlider.Value = color.B;
                    });
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public FrostbiteColorizer()
        {
            InitializeComponent();
            _instance = this; // Initialize the static instance
            this.MouseMove += new MouseEventHandler(Window_MouseMove);
            this.MouseDown += new MouseButtonEventHandler(Window_MouseDown);
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

        private void btnEyedropper_Click(object sender, RoutedEventArgs e)
        {
            if (isEyedropperActive)
            {
                // Deactivate
                isEyedropperActive = false;
                UnhookWindowsHookEx(_hookID);
                this.Cursor = Cursors.Arrow;
                this.Topmost = false;  // Make the window not topmost
            }
            else
            {
                // Activate
                isEyedropperActive = true;
                _hookID = SetHook(_proc);
                this.Cursor = Cursors.Cross;
                this.Topmost = true;  // Make the window topmost
            }
        }

        public static Color GetPixelColor(int x, int y)
        {
            IntPtr hdc = GetWindowDC(GetDesktopWindow());
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(1, 1);
            System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp);
            IntPtr hdcDest = g.GetHdc();
            BitBlt(hdcDest, 0, 0, 1, 1, hdc, x, y, (int)System.Drawing.CopyPixelOperation.SourceCopy);
            g.ReleaseHdc(hdcDest);
            g.Dispose();
            ReleaseDC(GetDesktopWindow(), hdc);
            System.Drawing.Color pixel = bmp.GetPixel(0, 0);
            bmp.Dispose();
            return Color.FromRgb(pixel.R, pixel.G, pixel.B);
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (isEyedropperActive)
            {
                if (GetCursorPos(out POINT point))
                {
                    Color color = GetPixelColor(point.X, point.Y);
                    rSlider.Value = color.R;
                    gSlider.Value = color.G;
                    bSlider.Value = color.B;
                }
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (isEyedropperActive)
            {
                // Deactivate the eyedropper tool
                isEyedropperActive = false;
                this.Cursor = Cursors.Arrow; // Restore default cursor
            }
        }
    }
}
