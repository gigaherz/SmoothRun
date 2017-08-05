using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SmoothRun.Util
{
    [ValueConversion(typeof(string), typeof(ImageSource))]
    public class FileToIconConverter : IValueConverter
    {
        #region IMultiValueConverter Members
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            using (Icon ico = Icon.FromHandle(ExtractFileIcon.ReadIcon(value as string ?? "")))
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    ico.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion

    }
}
