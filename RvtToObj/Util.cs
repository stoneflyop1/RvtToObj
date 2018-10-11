using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Diagnostics;
using WinForms = System.Windows.Forms;

namespace RvtToObj
{
    class Util
    {
        const string _caption = "RevitToObj";
        public static void ErrorMsg(string msg)
        {
            Debug.WriteLine(msg);
            WinForms.MessageBox.Show(msg,
                                    _caption,
                                    WinForms.MessageBoxButtons.OK,
                                    WinForms.MessageBoxIcon.Error
                                    );
        }

        public static string RealString(double a)
        {
            return a.ToString("0.##########");
        }

        public static string PointString(XYZ p)
        {
            return string.Format("({0},{1},{2})",
              RealString(p.X),
              RealString(p.Y),
              RealString(p.Z));
        }

        public static int ColorToInt(Color color)
        {
            return color.Red << 16 | color.Green << 8 | color.Blue;
        }

        public static int ColorTransparencyToInt(Color color, int transparency)
        {
            Debug.Assert(0 <= transparency, "expected non-negative transparency");
            Debug.Assert(100 >= transparency, "expected transparency between 0 and 100");
            uint trgb = ((uint)transparency << 24) | (uint)ColorToInt(color);
            Debug.Assert(int.MaxValue > trgb, "expected trgb smaller than max int");
            return (int)trgb;
        }

        static string ColorString(Color color)//指定X2，16进制显示的更整齐
        {
            return color.Red.ToString("X2") + color.Green.ToString("X2") + color.Blue.ToString("X2");
        }
        public static string ColorTransparencyString(Color color, int transparency)
        {
            return transparency.ToString("X2") + ColorString(color);
        }

        static Color IntToColor(int rgb)
        {
            return new Color((byte)((rgb & 0xFF0000) >> 16), (byte)((rgb & 0xFF00) >> 8), (byte)(rgb & 0xFF));
        }
        public static Color IntToColorTransparency(int trgb, out int transparency)
        {
            transparency = (int)((((uint)trgb) & 0xFF000000) >> 24);
            return IntToColor(trgb);
        }
    }
}
