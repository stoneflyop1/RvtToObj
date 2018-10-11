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
        const string _mtl_newmtl_d
            = "newmtl {0}\r\n"
            + "ka {1} {2} {3}\r\n"
            + "Kd {1} {2} {3}\r\n"
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

            const double _eps = 1.0e-9;

            const double _feet_to_mm = 25.4 * 12;

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

            class PointDoubleEqualityComparer : IEqualityComparer<PointDouble>
            {
                public bool Equals(PointDouble p, PointDouble q)
                {
                    return 0 == p.CompareTo(q);
                }

                public int GetHashCode(PointDouble p)
                {
                    //return (p.X.ToString()
                    //  + "," + p.Y.ToString()
                    //  + "," + p.Z.ToString())
                    //  .GetHashCode();
                    var format = "{0.#########}";
                    return (p.X.ToString(format)
                      + "," + p.Y.ToString(format)
                      + "," + p.Z.ToString(format))
                      .GetHashCode();
                }
            }
            #endregion // PointIntEqualityComparer

            public VertexLookupDouble()
              : base(new PointDoubleEqualityComparer())
            {
            }

            public int AddVertex(PointDouble p)
            {
                return ContainsKey(p)
                  ? this[p]
                  : this[p] = Count;
            }
        }

        class VertexLookupInt : Dictionary<PointInt, int>
        {
            #region PointIntEqualityComparer

            class PointIntEqualityComparer : IEqualityComparer<PointInt>
            {
                public bool Equals(PointInt p, PointInt q)
                {
                    return 0 == p.CompareTo(q);
                }

                public int GetHashCode(PointInt p)
                {  
                    return (p.X.ToString()
                      + "," + p.Y.ToString()
                      + "," + p.Z.ToString())
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

        //材质信息
        Color currentColor;
        int currentTransparencyint;
        double currentTransparencyDouble;
        int currentShiniess;
        ElementId currentMterialId = ElementId.InvalidElementId;
        int materialIndex = 0;
        Dictionary<string, Color> colors = new Dictionary<string, Color>();
        Dictionary<string, double> transparencys = new Dictionary<string, double>();
        Asset currentAppearance = null;

        //几何信息
        List<int> face = new List<int>();
        VertexLookupInt _vertices = new VertexLookupInt();
        VertexLookupDouble _uvs = new VertexLookupDouble();
        VertexLookupDouble _normals = new VertexLookupDouble();
        Dictionary<string, Class1.Material> _materials = new Dictionary<string, Class1.Material>();

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
            currentTransparencyDouble = node.Transparency;
            currentColor = node.Color;
            currentShiniess = node.Glossiness;
            currentTransparencyint = Convert.ToInt32(node.Transparency);
            

            if (node.MaterialId != ElementId.InvalidElementId)
            {
                Element m = _doc.GetElement(node.MaterialId);
                Material material = _doc.GetElement(m.UniqueId) as Material;
                ElementId appearanceId = material.AppearanceAssetId;
                AppearanceAssetElement appearanceElem = _doc.GetElement(appearanceId) as AppearanceAssetElement;
                Asset theAsset = appearanceElem.GetRenderingAsset();

                int size = theAsset.Size;              
                for (int assetIdx = 0; assetIdx < size; assetIdx++)
                {
                    AssetProperty aProperty = theAsset[assetIdx];
                    if (aProperty.NumberOfConnectedProperties < 1)
                        continue;
                    // Find first connected property.
                    // Should work for all current (2018) schemas.
                    // Safer code would loop through all connected
                    // properties based on the number provided.
                    Asset connectedAsset = aProperty.GetConnectedProperty(0) as Asset;
                    // We are only checking for bitmap connected assets.
                    if (connectedAsset.Name == "UnifiedBitmapSchema")
                    {
                        // This line is 2018.1 & up because of the
                        // property reference to UnifiedBitmap
                        // .UnifiedbitmapBitmap. In earlier versions,
                        // you can still reference the string name
                        // instead: "unifiedbitmap_Bitmap"
                        AssetPropertyString path = connectedAsset["unifiedbitmap_Bitmap"] as AssetPropertyString;
                        // This will be a relative path to the
                        // built -in materials folder, addiitonal
                        // render appearance folder, or an
                        // absolute path.
                        TaskDialog.Show("Connected bitmap", String.Format("{0} from {2}: {1}", aProperty.Name, path.Value, connectedAsset.LibraryName));
                    }
                }
            }

            if (currentMterialId != node.MaterialId)
            {
                var trgb = Util.ColorTransparencyToInt(currentColor, currentTransparencyint);
                face.Add(-1);
                face.Add(trgb);
                face.Add(currentTransparencyint);
                face.Add(-2);
                face.Add(-2);
                face.Add(-2);
                face.Add(-2);
                face.Add(-2);
                face.Add(-2);
                currentMterialId = node.MaterialId;


                var ttrgb = Util.ColorTransparencyString(currentColor, currentTransparencyint);

                if (!transparencys.ContainsKey(ttrgb))
                {
                    transparencys.Add(ttrgb, 1.0 - currentTransparencyDouble);
                }

                if (!colors.ContainsKey(ttrgb))
                {
                    colors.Add(ttrgb, currentColor);
                }              
            }
            else
            {
                if (materialIndex == 0)
                {
                    var trgb = Util.ColorTransparencyToInt(currentColor, currentTransparencyint);
                    face.Add(-1);
                    face.Add(trgb);
                    face.Add(currentTransparencyint);
                    face.Add(-2);
                    face.Add(-2);
                    face.Add(-2);
                    face.Add(-2);
                    face.Add(-2);
                    face.Add(-2);
                    currentMterialId = node.MaterialId;
                    var ttrgb = Util.ColorTransparencyString(currentColor, currentTransparencyint);
                    colors.Add(ttrgb, currentColor);
                    transparencys.Add(ttrgb, currentTransparencyint);
                }
            }
            materialIndex++;

            if (node.HasOverriddenAppearance)
            {
                currentAppearance = node.GetAppearanceOverride();
            }
            else
            {
                currentAppearance = node.GetAppearance();
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
            IList<XYZ> pts = polymesh.GetPoints();
            Transform t = CurrentTransform;
            pts = pts.Select(p => t.OfPoint(p)).ToList();

            var normals = polymesh.GetNormals();
            var uvs = polymesh.GetUVs();

            int v1, v2, v3;
            int v4, v5, v6;
            int v7, v8, v9;
            int faceindex=0;

            foreach (PolymeshFacet facet in polymesh.GetFacets())
            {

                v1 = _vertices.AddVertex(new PointInt(pts[facet.V1], _switch_coordinates));
                v2 = _vertices.AddVertex(new PointInt(pts[facet.V2], _switch_coordinates));
                v3 = _vertices.AddVertex(new PointInt(pts[facet.V3], _switch_coordinates));

                face.Add(v1);
                face.Add(v2);
                face.Add(v3);

                v4 = _uvs.AddVertex(new PointDouble(uvs[facet.V1]));
                v5 = _uvs.AddVertex(new PointDouble(uvs[facet.V2]));
                v6 = _uvs.AddVertex(new PointDouble(uvs[facet.V3]));
                face.Add(v4);
                face.Add(v5);
                face.Add(v6);

                if (polymesh.DistributionOfNormals == DistributionOfNormals.AtEachPoint)
                {
                    v7 = _normals.AddVertex(new PointDouble(normals[facet.V1], _switch_coordinates));
                    v8 = _normals.AddVertex(new PointDouble(normals[facet.V2], _switch_coordinates));
                    v9 = _normals.AddVertex(new PointDouble(normals[facet.V3], _switch_coordinates));
                }
                else if (polymesh.DistributionOfNormals == DistributionOfNormals.OnEachFacet)
                {
                    v7 = _normals.AddVertex(new PointDouble(normals[faceindex], _switch_coordinates));
                    v8 = v7;
                    v9 = v7;
                }
                else
                {
                    v7 = _normals.AddVertex(new PointDouble(normals[0],_switch_coordinates));
                    v8 = v7;
                    v9 = v7;
                }          
                face.Add(v7);
                face.Add(v8);
                face.Add(v9);

                faceindex++;
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
                s.WriteLine(_mtl_mtllib, "model.mtl");

                foreach (PointInt key in _vertices.Keys)
                {
                    s.WriteLine(_mtl_vertex, key.X / 1000, key.Y / 1000, key.Z / 1000);
                }

                foreach (PointDouble key in _normals.Keys)
                {
                    s.WriteLine(_mtl_normal, key.X, key.Y, key.Z);
                }

                foreach (PointDouble key in _uvs.Keys)
                {
                    s.WriteLine(_mtl_uv, key.X, key.Y);
                }
                
                int i = 0;
                int n = face.Count;
                while (i < n)
                {
                    int i1 = face[i++];
                    int i2 = face[i++];
                    int i3 = face[i++];

                    int i4 = face[i++];
                    int i5 = face[i++];
                    int i6 = face[i++];

                    int i7 = face[i++];
                    int i8 = face[i++];
                    int i9 = face[i++];
                    if (-1==i1)
                    {
                        s.WriteLine($"usemtl {Util.ColorTransparencyString(Util.IntToColorTransparency(i2,out i3),i3)}");
                    }
                    else
                    {
                        s.WriteLine($"f {i1 + 1}/{i4 + 1}/{i7 + 1} {i2 + 1}/{i5 + 1}/{i8 + 1} {i3 + 1}/{i6 + 1}/{i9 + 1}");
                    }
                    
                }
            }
            using (StreamWriter s = new StreamWriter(Path.GetDirectoryName(_filename) + "\\model.mtl"))
            {
                foreach (KeyValuePair<string, Color> color in colors)
                {
                    s.WriteLine(_mtl_newmtl_d, 
                                color.Key,
                                color.Value.Red/256.0,
                                color.Value.Green/256.0,
                                color.Value.Blue/256.0, 
                                transparencys[color.Key]);
                }
            }
            TaskDialog.Show("RvtToObj", "导出成功！");
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