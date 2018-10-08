using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Diagnostics;
using System.IO;
using Autodesk.Revit.Utility;
using System.Linq;
using System.Collections.Generic;

namespace RvtToObj
{
    internal class RvtExportContext : IExportContext
    {

        #region mtl statement format strings
        const string _mtl_newmtl
            = "newmtl {0}\r\n"
            + "ka {1} {2} {3}\r\n"
            + "Kd {1} {2} {3}\r\n"
            + "Ks {1} {2} {3}\r\n"
            + "Ns {4}";
        const string _mtl_newmtl_d
            = "newmtl {0}\r\n"
            + "ka {1} {2} {3}\r\n"
            + "Kd {1} {2} {3}\r\n"
            + "Ks {1} {2} {3}\r\n"
            + "d {4}";

        const string _mtl_mtllib = "mtllib {0}";
        const string _mtl_usemtl = "usemtl {0}";
        const string _mtl_vertex = "v {0} {1} {2}";
        const string _mtl_face = "f {0}/{3}/{6} {1}/{4}/{7} {2}/{5}/{8}";
        const string _mtl_normal = "vn {0} {1} {2}";
        const string _mtl_uv = "vt {0} {1}";
        #endregion

        #region VertexLookupXyz
        /// <summary>
        /// A vertex lookup class to eliminate 
        /// duplicate vertex definitions.
        /// </summary>
        class VertexLookupXyz : Dictionary<XYZ, int>
        {
            #region XyzEqualityComparer
            /// <summary>
            /// Define equality for Revit XYZ points.
            /// Very rough tolerance, as used by Revit itself.
            /// </summary>
            class XyzEqualityComparer : IEqualityComparer<XYZ>
            {
                const double _sixteenthInchInFeet
                  = 1.0 / (16.0 * 12.0);

                public bool Equals(XYZ p, XYZ q)
                {
                    return p.IsAlmostEqualTo(q,
                      _sixteenthInchInFeet);
                }

                public int GetHashCode(XYZ p)
                {
                    return Util.PointString(p).GetHashCode();
                }
            }
            #endregion // XyzEqualityComparer

            public VertexLookupXyz()
              : base(new XyzEqualityComparer())
            {
            }

            /// <summary>
            /// Return the index of the given vertex,
            /// adding a new entry if required.
            /// </summary>
            public int AddVertex(XYZ p)
            {
                return ContainsKey(p)
                  ? this[p]
                  : this[p] = Count;
            }
        }
        #endregion // VertexLookupXyz

        #region VertexLookupInt
        /// <summary>
        /// An integer-based 3D point class.
        /// </summary>

        class PointDouble : IComparable<PointDouble>
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }

            const double _eps = 1.0e-9;

            public PointDouble(UV p)
            {
                X = p.U;
                Y = p.V;
            }
            public PointDouble(XYZ p, bool switch_coordinates)
            {
                X = p.X;
                Y = p.Y;
                Z = p.Z;

                if (switch_coordinates)
                {
                    X = X;
                    double tmp = Y;
                    Y = Z;
                    Z = -tmp;
                }
            }

            private static int CompareDouble(double a, double b)
            {
                if (Math.Abs(a - b) < _eps) return 0;
                if (a > b) return 1;
                return -1;
            }

            public int CompareTo(PointDouble a)
            {
                var d = CompareDouble(X, a.X);
                if (d != 0) return d;
                d = CompareDouble(Y, a.Y);
                if (d != 0) return d;
                return CompareDouble(Z, a.Z);

            }
        }

        class PointInt : IComparable<PointInt>, IEquatable<PointInt>
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }


            //public PointInt( int x, int y, int z )
            //{
            //  X = x;
            //  Y = y;
            //  Z = z;
            //}

            /// <summary>
            /// Consider a Revit length zero 
            /// if is smaller than this.
            /// </summary>
            const double _eps = 1.0e-9;

            /// <summary>
            /// Conversion factor from feet to millimetres.
            /// </summary>
            const double _feet_to_mm = 25.4 * 12;

            /// <summary>
            /// Conversion a given length value 
            /// from feet to millimetre.
            /// </summary>
            static double ConvertFeetToMillimetres(double d)
            {
                return _feet_to_mm * d;
            }

            public PointInt(XYZ p, bool switch_coordinates)
            {
                X = ConvertFeetToMillimetres(p.X);
                Y = ConvertFeetToMillimetres(p.Y);
                Z = ConvertFeetToMillimetres(p.Z);

                if (switch_coordinates)
                {
                    X = X;
                    var tmp = Y;
                    Y = Z;
                    Z = -tmp;
                }
            }



            private static int CompareDouble(double a, double b)
            {
                if (Math.Abs(a - b) < _eps) return 0;
                if (a > b) return 1;
                return -1;
            }
            public int CompareTo(PointInt a)
            {
                var d = CompareDouble(X, a.X);
                if (d != 0) return d;
                d = CompareDouble(Y, a.Y);
                if (d != 0) return d;
                return CompareDouble(Z, a.Z);
            }

            public bool Equals(PointInt other)
            {
                if (other == null) return false;
                return Math.Abs(X - other.X) < _eps && Math.Abs(Y - other.Y) < _eps && Math.Abs(Z - other.Z) < _eps;
            }

            public override bool Equals(object obj)
            {
                var o = obj as PointInt;
                return Equals(o);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

        }

        class VertexLookupDouble : Dictionary<PointDouble, int>
        {
            #region PointIntEqualityComparer
            /// <summary>
            /// Define equality for integer-based PointInt.
            /// </summary>
            class PointDoubleEqualityComparer : IEqualityComparer<PointDouble>
            {
                public bool Equals(PointDouble p, PointDouble q)
                {
                    return 0 == p.CompareTo(q);
                }

                public int GetHashCode(PointDouble p)
                {
                    return (p.X.ToString()
                      + "," + p.Y.ToString()
                      + "," + p.Z.ToString())
                      .GetHashCode();
                }
            }
            #endregion // PointIntEqualityComparer

            public VertexLookupDouble()
              : base(new PointDoubleEqualityComparer())
            {
            }

            /// <summary>
            /// Return the index of the given vertex,
            /// adding a new entry if required.
            /// </summary>
            public int AddVertex(PointDouble p)
            {
                return ContainsKey(p)
                  ? this[p]
                  : this[p] = Count;
            }
        }


        /// <summary>
        /// A vertex lookup class to eliminate 
        /// duplicate vertex definitions.
        /// </summary>
        class VertexLookupInt : Dictionary<PointInt, int>
        {
            #region PointIntEqualityComparer
            /// <summary>
            /// Define equality for integer-based PointInt.
            /// </summary>
            class PointIntEqualityComparer : IEqualityComparer<PointInt>
            {
                public bool Equals(PointInt p, PointInt q)
                {
                    return 0 == p.CompareTo(q);
                }

                public int GetHashCode(PointInt p)
                {
                    var format = "{0.#########}";
                    return (p.X.ToString(format)
                      + "," + p.Y.ToString(format)
                      + "," + p.Z.ToString(format))
                      .GetHashCode();
                }
            }
            #endregion // PointIntEqualityComparer

            public VertexLookupInt()
              : base(new PointIntEqualityComparer())
            {
            }

            /// <summary>
            /// Return the index of the given vertex,
            /// adding a new entry if required.
            /// </summary>
            public int AddVertex(PointInt p)
            {
                return ContainsKey(p)
                  ? this[p]
                  : this[p] = Count;
            }
        }
        #endregion // VertexLookupInt

        int i = 0;
        int j = 0;
        int k = 0;
        List<XYZ> pp = new List<XYZ>();
        List<XYZ> nn = new List<XYZ>();
        List<UV> uu = new List<UV>();
        List<int> face = new List<int>();
        VertexLookupInt vpts = new VertexLookupInt();
        VertexLookupDouble vuvs = new VertexLookupDouble();
        VertexLookupDouble vnormals = new VertexLookupDouble();

        bool _switch_coordinates =true;
        Document _doc;
        string _filename;
        Stack<Transform> _transformationStack = new Stack<Transform>();


        Transform CurrentTransform
        {
            get
            {
                return _transformationStack.Peek();
            }
        }

        public RvtExportContext(Document doc, string filename)
        {
            this._doc = doc;
            this._filename = filename;
        }

        public bool IsCanceled()
        {
            //TaskDialog.Show("revit", "IsCanceled");
            return false;
        }

        public bool Start()
        {
            _transformationStack.Push(Transform.Identity);
            //TaskDialog.Show("revit", "Start");
            return true;
        }

        public RenderNodeAction OnViewBegin(ViewNode node)
        {
            //TaskDialog.Show("revit", "OnViewBegin");
            return RenderNodeAction.Proceed;
        }

        public RenderNodeAction OnElementBegin(ElementId elementId)
        {
            //TaskDialog.Show("revit", "OnElementBegin");

            Element e = _doc.GetElement(elementId);
            string uid = e.UniqueId;
            return RenderNodeAction.Proceed;
        }

        public RenderNodeAction OnInstanceBegin(InstanceNode node)
        {
            //TaskDialog.Show("revit", "OnInstanceBegin");
            _transformationStack.Push(CurrentTransform.Multiply(node.GetTransform()));
            return RenderNodeAction.Proceed;
        }

        public void OnMaterial(MaterialNode node)
        {
            //TaskDialog.Show("revit", "OnMaterial");

            ElementId id = node.MaterialId;

            if (ElementId.InvalidElementId != id)
            {
                Element m = _doc.GetElement(node.MaterialId);

            }
        }

        public RenderNodeAction OnFaceBegin(FaceNode node)
        {
            //TaskDialog.Show("revit", "OnFaceBegin");
            Debug.WriteLine(" OnFaceBegin: " + node.NodeName);
            return RenderNodeAction.Proceed;
        }
        
        public void OnPolymesh(PolymeshTopology polymesh)
        {
            i += polymesh.NumberOfPoints;
            j += polymesh.NumberOfNormals;
            k += polymesh.NumberOfUVs;

            IList<XYZ> pts = polymesh.GetPoints();
            Transform t = CurrentTransform;
            pts = pts.Select(p => t.OfPoint(p)).ToList();
           
            for (int i=0; i < polymesh.NumberOfPoints; i++)
            {
               
                pp.Add(pts[i]);
            }

            var normals = polymesh.GetNormals();
            for (int j=0; j<polymesh.NumberOfNormals; j++)
            {
                nn.Add(normals[j]);
            }


            var uvs = polymesh.GetUVs();
            for (int k = 0; k < polymesh.NumberOfUVs; k++)
            {
                uu.Add(uvs[k]);
            }

            int v1, v2, v3;
            int v4, v5, v6;
            int v7, v8, v9;
            int faceindex=0;
            foreach (PolymeshFacet facet in polymesh.GetFacets())
            {
        
                v1 = vpts.AddVertex(new PointInt(pts[facet.V1], _switch_coordinates));
                v2 = vpts.AddVertex(new PointInt(pts[facet.V2], _switch_coordinates));
                v3 = vpts.AddVertex(new PointInt(pts[facet.V3], _switch_coordinates));

                face.Add(v1);
                face.Add(v2);
                face.Add(v3);

                v4 = vuvs.AddVertex(new PointDouble(uvs[facet.V1]));
                v5 = vuvs.AddVertex(new PointDouble(uvs[facet.V2]));
                v6 = vuvs.AddVertex(new PointDouble(uvs[facet.V3]));

                face.Add(v4);
                face.Add(v5);
                face.Add(v6);

                if (polymesh.DistributionOfNormals == DistributionOfNormals.AtEachPoint)
                {
                    v7 = vnormals.AddVertex(new PointDouble(normals[facet.V1], _switch_coordinates));
                    v8 = vnormals.AddVertex(new PointDouble(normals[facet.V2], _switch_coordinates));
                    v9 = vnormals.AddVertex(new PointDouble(normals[facet.V3], _switch_coordinates));

                    
                }
                else if (polymesh.DistributionOfNormals == DistributionOfNormals.OnEachFacet)
                {
                    v7 = vnormals.AddVertex(new PointDouble(normals[faceindex], _switch_coordinates));
                    v8 = v7;
                    v9 = v7;
                }
                else
                {
                    v7 = vnormals.AddVertex(new PointDouble(normals[0],_switch_coordinates));
                    v8 = v7;
                    v9 = v7;
                }
                face.Add(v7);
                face.Add(v8);
                face.Add(v9);

                faceindex++;
                //TaskDialog.Show("r", "aaaa");
                //TaskDialog.Show("RVT",face.Count.ToString());
            }
        }

        public void OnFaceEnd(FaceNode node)
        {
            //TaskDialog.Show("revit", "OnFaceEnd");
            Debug.WriteLine(" OnFaceEnd: " + node.NodeName);
        }

        public void OnInstanceEnd(InstanceNode node)
        {
            //TaskDialog.Show("revit", "OnInstanceEnd");
            Debug.WriteLine(" OnInstanceEnd: " + node.NodeName);
            _transformationStack.Pop();
        }

        public void OnElementEnd(ElementId id)
        {
            //TaskDialog.Show("revit", "OnElementEnd");
            Element e = _doc.GetElement(id);
            string uid = e.UniqueId;
        }

        public void OnViewEnd(ElementId elementId)
        {
            //TaskDialog.Show("revit", "OnViewEnd");
            Debug.WriteLine("OnViewEnd: Id: " + elementId.IntegerValue);
        }

        public void Finish()
        {
            //TaskDialog.Show("revit", "Finish");
            

            string material_library_path = null;
            material_library_path = Path.ChangeExtension(_filename, "mtl");
            using (StreamWriter s = new StreamWriter(_filename))
            {
                s.WriteLine(_mtl_mtllib, Path.GetFileName(material_library_path).ToString());


                foreach (var p in pp)
                {
                    PointInt pt = new PointInt(p, _switch_coordinates);
                    s.WriteLine(_mtl_vertex, pt.X/1000, pt.Y/1000, pt.Z/1000);
                }

                foreach (var n in nn)
                {
                    var ptt = new PointDouble(n, _switch_coordinates);
                    s.WriteLine(_mtl_normal, ptt.X, ptt.Y, ptt.Z);
                }

                foreach (var u in uu)
                {
                    var uv = new PointDouble(u);
                    s.WriteLine(_mtl_uv, uv.X,uv.Y);
                }
                TaskDialog.Show("rvt", face.Count.ToString());
                for (int i=0; i<face.Count; i+=9)
                {
                    s.WriteLine($"f {face[i]} {face[i + 1]} {face[i + 2]}");
                    //s.WriteLine(_mtl_face, face[i], face[i+1], face[i+2],
                    //                       face[i+3], face[i+4], face[i+5],
                    //                       face[i+6], face[i+7], face[i+8]
                    //            );
                }
            }
            TaskDialog.Show("to_obj","vertices:" + i.ToString()+ "  normals:" + j.ToString() + "  uvs:"+ k.ToString());
        }

        public void OnDaylightPortal(DaylightPortalNode node)
        {
            //TaskDialog.Show("revit", "OnDaylightPortal");
            {
                Debug.WriteLine("OnDaylightPortal: " + node.NodeName);
                Asset asset = node.GetAsset();
                Debug.WriteLine("OnDaylightPortal: Asset:"
                + ((asset != null) ? asset.Name : "Null"));
            }
        }

        public void OnLight(LightNode node)
        {
            //TaskDialog.Show("revit", "OnLight");
            Debug.WriteLine("OnLight: " + node.NodeName);
        }

        public RenderNodeAction OnLinkBegin(LinkNode node)
        {
            //TaskDialog.Show("revit", "OnLinkBegin");
            Debug.WriteLine(" OnLinkBegin: " + node.NodeName + " Document: " + node.GetDocument().Title + ": Id: " + node.GetSymbolId().IntegerValue);
            _transformationStack.Push(CurrentTransform.Multiply(node.GetTransform()));
            return RenderNodeAction.Proceed;
        }

        public void OnLinkEnd(LinkNode node)
        {
            //TaskDialog.Show("revit", "OnLinkEnd");
            Debug.WriteLine(" OnLinkEnd: " + node.NodeName);
            _transformationStack.Pop();
        }

        public void OnRPC(RPCNode node)
        {
            //TaskDialog.Show("revit", "OnRPC");
            Debug.WriteLine("OnRPC: " + node.NodeName);
        }
    }
}