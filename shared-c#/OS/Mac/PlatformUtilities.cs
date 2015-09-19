using System;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Runtime.InteropServices;
using AppInstall.UI;
using Foundation;
using CoreBluetooth;
using UIKit;

namespace AppInstall.Framework
{
    public static class PlatformUtilities
    {

        public static byte[] ToByteArray(this NSData data)
        {
            if (data == null) return null;
            var newData = new byte[data.Length];
            if (data.Length == 0) return newData;
            Marshal.Copy(data.Bytes, newData, 0, Convert.ToInt32(data.Length));
            return newData;
        }

        public static Guid ToGuid(this CBUUID uuid)
        {
            return AppInstall.Hardware.Bluetooth.MakeGuid(uuid.ToString());
        }

        public static DateTime ToDateTime(this NSDate date)
        {
            // NSDate has a wider range than DateTime, so clip
            // the converted date to DateTime.Min|MaxValue.
            double secs = date.SecondsSinceReferenceDate;
            if (secs < -63113904000)
                return DateTime.MinValue;
            if (secs > 252423993599)
                return DateTime.MaxValue;
            return (DateTime)date;
        }

        public static NSDate ToNSDate(this DateTime date)
        {
            if (date.Kind == DateTimeKind.Unspecified)
                //    date = DateTime.SpecifyKind(date, /* DateTimeKind.Local or DateTimeKind.Utc, this depends on each app */);
                throw new Exception("is this conversion local or UTC?");
            return (NSDate)date;
        }


        public static Exception ToException(this NSError error)
        {
            StringBuilder userInfo = new StringBuilder();
            if (error.UserInfo != null)
                foreach (var info in error.UserInfo.ToList())
                    userInfo.AppendLine(info.Key.ToString() + ": " + info.Value.ToString());
            return new Exception("NSError: " + error.DebugDescription + ", \nUserInfo: \n" + userInfo.ToString());
        }

        /// <summary>
        /// Returns the size that the specified string would take up if drawn within the specified bounds.
        /// </summary>
        public static Vector2D<float> MeasureStringSize(string str, UIFont font, Vector2D<float> maxSize)
        {
            Vector2D<float> s = new NSString(str).GetBoundingRect(new SizeF(maxSize.X, maxSize.Y), NSStringDrawingOptions.DisableScreenFontSubstitution | NSStringDrawingOptions.UsesLineFragmentOrigin | NSStringDrawingOptions.UsesFontLeading, new UIStringAttributes() { Font = font }, null).Size.ToVector2D();
            return new Vector2D<float>(s.X, s.Y);
        }

        /// <summary>
        /// Returns the largest size that the specified strings would take up if drawn within the specified bounds.
        /// Both X and Y dimensions are chosen separately from the largest value.
        /// </summary>
        /// <param name="strings">The strings that should be measured. Null or whitespace strings are ignored.</param>
        public static Vector2D<float> MeasureStringSize(UIFont font, Vector2D<float> maxSize, params string[] strings)
        {
            return (from str in strings where !string.IsNullOrWhiteSpace(str) select MeasureStringSize(str, font, maxSize)).Max();
        }
        


        /// <summary>
        /// Swaps X and Y and Width and Height of a rectangle if the orientation is landscape
        /// </summary>
        [Obsolete("justification is done at the very top of the view hierarchy (also what about the function in Platform?)")]
        public static RectangleF JustifyOrientation(this RectangleF rectangle, UIInterfaceOrientation orientation)
        {
            if ((orientation == UIInterfaceOrientation.Portrait) || (orientation == UIInterfaceOrientation.PortraitUpsideDown)) return rectangle;
            return new RectangleF(rectangle.Y, rectangle.X, rectangle.Height, rectangle.Width);
        }
    }
}