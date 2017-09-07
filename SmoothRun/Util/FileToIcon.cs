using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SmoothRun.Util
{
    [ValueConversion(typeof(string), typeof(ImageSource))]
    public class FileToIconConverter : IValueConverter
    {
#region IMultiValueConverter Members
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            int size;
            if (!int.TryParse(parameter as string ?? "", out size))
                size = 128;
            IntPtr hBitmap = WindowsThumbnailProvider.GetHBitmap(value as string ?? "", size, size, ThumbnailOptions.None);
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                WindowsThumbnailProvider.DeleteObject(hBitmap);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

#endregion

    }
}
