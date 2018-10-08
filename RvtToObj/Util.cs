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
    }
}
