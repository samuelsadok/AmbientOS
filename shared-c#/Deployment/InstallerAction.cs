using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using AppInstall.OS;
using AppInstall.Framework;

namespace AppInstall.Installer
{
    [XmlInclude(typeof(InstallerInsertFileAction))]
    [XmlInclude(typeof(InstallerDeleteFileAction))]
    public abstract class InstallerAction
    {
        /// <summary>
        /// This routine must take care of all preparations
        /// </summary>
        public abstract void Prepare(InstallerContext context, CancellationToken cancellationToken);
        public abstract void Execute(InstallerContext context);
        public virtual bool HasOppositeEffect(InstallerAction action)
        {
            return false;
        }
    }

    /// <summary>
    /// This abstract class represents an action involving a specific file or directory
    /// </summary>
    public abstract class InstallerFileAction : InstallerAction
    {
        public enum PathRootType
        {
            Absolute,
            BinaryPath,
            Temp,
            ApplicationPath
        }

        public Guid Guid { get; set; }
        public bool IsFolder { get; set; }
        public PathRootType PathRoot { get; set; }
        public string RelativePath { get; set; }

        public string GetAbsolutePath(InstallerContext context)
        {
            switch (PathRoot) {
                case InstallerFileAction.PathRootType.Absolute : return Path.GetFullPath(RelativePath);
                case InstallerFileAction.PathRootType.ApplicationPath: return Path.GetFullPath(context.ApplicationPath + "\\" + RelativePath);
                case InstallerFileAction.PathRootType.BinaryPath: return Path.GetFullPath(Directory.GetParent(context.ApplicationBinaryPath) + "\\" + RelativePath);
                case InstallerFileAction.PathRootType.Temp: return Path.GetFullPath(Platform.TempFolder + "\\" + RelativePath);
                default: throw new Exception("unknown path root type");
            }
        }
    }

    /// <summary>
    /// This action creates a directory or places a file that is downloaded in the prepare step.
    /// </summary>
    public class InstallerInsertFileAction : InstallerFileAction
    {
        public bool Overwrite { get; set; }

        public override void Prepare(InstallerContext context, CancellationToken cancellationToken)
        {
            if (!IsFolder) {
                context.LogContext.Log("downloading file " + Guid);
                Task<byte[]> t = context.SoftwareServerClient.DownloadFile(Guid, cancellationToken);
                using (FileStream file = File.Create(context.InstallerFolder + "\\" + Guid)) {
                    t.Wait();
                    file.Write(t.Result, cancellationToken).Wait();
                }
            }
        }

        public override void Execute(InstallerContext context)
        {
            if (IsFolder) {
                Utilities.CreateDirectory(GetAbsolutePath(context));
            } else {
                Utilities.CreateDirectory(Directory.GetParent(GetAbsolutePath(context)).FullName);
                context.LogContext.Log("copying from " + context.InstallerFolder + "\\" + Guid + " to " + GetAbsolutePath(context));
                File.Copy(context.InstallerFolder + "\\" + Guid, GetAbsolutePath(context), Overwrite);
            }
        }
        public override bool HasOppositeEffect(InstallerAction action)
        {
            var deleteAction = action as InstallerDeleteFileAction;
            if (deleteAction == null) return false;
            return (deleteAction.Guid == Guid && deleteAction.PathRoot == PathRoot && deleteAction.RelativePath == RelativePath && deleteAction.IsFolder == IsFolder);
        }
    }

    /// <summary>
    /// This action deletes the specified file or directory and its contents when executed. Empty parent directories are removed recursively.
    /// </summary>
    public class InstallerDeleteFileAction : InstallerFileAction
    {
        public override void Prepare(InstallerContext context, CancellationToken cancellationToken)
        {
            // no preparation required
        }

        public override void Execute(InstallerContext context)
        {
            if (IsFolder)
                Directory.Delete(GetAbsolutePath(context), true);
            else
                File.Delete(GetAbsolutePath(context));
            Utilities.DeleteEmptyDirectory(Directory.GetParent(GetAbsolutePath(context)).FullName);
        }

        public override bool HasOppositeEffect(InstallerAction action)
        {
            var insertAction = action as InstallerInsertFileAction;
            if (insertAction == null) return false;
            return (insertAction.Guid == Guid && insertAction.PathRoot == PathRoot && insertAction.RelativePath == RelativePath && insertAction.IsFolder == IsFolder);
        }
    }

}
