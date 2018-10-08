using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RvtToObj
{
    class Class1
    {
        public struct RvtObj
        {
            public List<double> vertices { get; set; }
            public List<double> normals { get; set; }
            public List<double> uvs { get; set; }
            public List<int> faces { get; set; }
        }
        //public RvtObj Rvt;
    }
}
