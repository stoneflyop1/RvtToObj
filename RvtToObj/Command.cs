using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.IO;
using Autodesk.Revit.Utility;
using System.Collections.Generic;
using System.Text;
using System;
using System.Windows.Forms;

namespace RvtToObj
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand, IExternalCommandAvailability
    {
        string foldPath = string.Empty;
        public static Color DefaultColor = new Color(127, 127, 127);    //默认灰色

        /// <summary>
        /// 导出三维视图，调用CustomExporter.Export
        /// </summary>
        /// <param name="view3d"></param>
        /// <param name="filename"></param>
        public void ExportView3D(View3D view3d, string filename, AssetSet objlibraryAsset)
        {
            Document doc = view3d.Document;
            RvtExportContext context = new RvtExportContext(doc, filename, objlibraryAsset);
            CustomExporter exporter = new CustomExporter(doc, context);
            exporter.ShouldStopOnError = false;
            exporter.Export(view3d);
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Autodesk.Revit.ApplicationServices.Application app = uiapp.Application;
            Document doc = uidoc.Document;

            AssetSet objlibraryAsset = app.get_Assets(AssetType.Appearance);

            ///判断是否为空视图
            View3D view = doc.ActiveView as View3D;
            if (null == view)
            {
                Util.ErrorMsg("You must be in a 3D view to export.");
            }
            try
            {
                FolderBrowserDialog dialog = new FolderBrowserDialog();
                dialog.Description = "请选择文件保存路径";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    foldPath = dialog.SelectedPath;
                }
                string filename = doc.PathName;
                if (0 == filename.Length)
                {
                    filename = doc.Title;
                }
                filename = Path.GetFileNameWithoutExtension(filename) + ".obj";
                string subPath = foldPath + "\\" + Path.GetFileNameWithoutExtension(filename);
                if (!Directory.Exists(subPath))
                {
                    Directory.CreateDirectory(subPath);
                }
                filename = Path.Combine(subPath + "\\" + filename);

                ExportView3D(doc.ActiveView as View3D, filename, objlibraryAsset);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return Result.Succeeded;
        }

        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return true;
        }
    }
}
