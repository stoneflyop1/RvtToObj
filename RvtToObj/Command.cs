using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
using DialogResult = System.Windows.Forms.DialogResult;
using System.IO;

namespace RvtToObj
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        /// <summary>
        /// 导出三维视图，调用CustomExporter.Export
        /// </summary>
        /// <param name="view3d"></param>
        /// <param name="filename"></param>
        public void ExportView3D(View3D view3d,string filename)
        {
            Document doc = view3d.Document;

            RvtExportContext context = new RvtExportContext(doc, filename);

            CustomExporter exporter = new CustomExporter(doc, context);

            exporter.ShouldStopOnError = false;

            exporter.Export(view3d);
        }

        #region SelectFile
        static string _output_folder_path = null;

        static bool SelectFile(ref string folder_path,ref string filename)
        {
            SaveFileDialog dlg = new SaveFileDialog();

            dlg.Title = "Select OBJ Output File";
            dlg.Filter = "OBJ files|*.obj";

            if (null != folder_path && 0 < folder_path.Length)
            {
                dlg.InitialDirectory = folder_path;
            }

            dlg.FileName = filename;

            bool rc = DialogResult.OK == dlg.ShowDialog();
            if (rc)
            {
                filename = Path.Combine(dlg.InitialDirectory,dlg.FileName);
                folder_path = Path.GetDirectoryName(filename);
            }
            return rc;
        }
        #endregion // SelectFile

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Autodesk.Revit.ApplicationServices.Application app= uiapp.Application;
            Document doc = uidoc.Document;

            View3D view = doc.ActiveView as View3D;
            if (null == view)
            {
                Util.ErrorMsg("You must be in a 3D view to export.");
                return Result.Failed;
            }

            string filename = doc.PathName;

            if (0 == filename.Length)
            {
                filename = doc.Title;
            }

            if (null == _output_folder_path)
            {
                try
                {
                    _output_folder_path = Path.GetDirectoryName(filename);
                }
                catch
                {
                    TaskDialog.Show("Folder not found","Please save the file and run the command again.");
                    return Result.Failed;
                }
            }

            filename = Path.GetFileNameWithoutExtension(filename)+".obj";

            if (!SelectFile(ref _output_folder_path,ref filename))
            {
                return Result.Cancelled;
            }

            filename = Path.Combine(_output_folder_path,filename);

            ExportView3D(doc.ActiveView as View3D,filename);

            return Result.Succeeded;
        }
    }
}
