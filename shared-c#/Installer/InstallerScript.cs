using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using AppInstall.Framework;

namespace AppInstall.Installer
{
    //[XmlRoot(Namespace = AppInstall.Organization.GlobalConstants.HOMEPAGE)]
    public class InstallerScript
    {
        public Guid UpdaterGuid { get; set; }

        public Guid PackageID { get; set; }

        public InstallerContext Context { get; set; }

        public List<InstallerAction> Actions { get; set; }

        /// <summary>
        /// Prepares this script by downloading the required files and instatiating placeholders.
        /// </summary>
        public void Prepare(CancellationToken cancellationToken) // todo: make cancellable
        {
            foreach (var action in Actions) {
                Context.LogContext.Log("preparing item...");
                action.Prepare(Context, cancellationToken); // todo: call at the same tome
                Context.LogContext.Log("ok");
            }
        }

        /// <summary>
        /// Executes this script
        /// </summary>
        public void Execute() // todo: make cancellable
        {
            foreach (var action in Actions)
                action.Execute(Context);
        }


        //#region "XML Serialization"
        //
        //public static InstallerScript Deserialize(byte[] buffer)
        //{
        //    if (buffer == null) return null;
        //    using (MemoryStream stream = new MemoryStream(buffer))
        //        return InstallerScript.Deserialize(stream);
        //}
        //
        //public static InstallerScript Deserialize(Stream stream)
        //{
        //    XmlSerializer ser = new XmlSerializer(typeof(InstallerScript));
        //    return (InstallerScript)ser.Deserialize(stream);
        //}
        //
        //public void Serialize(Stream stream)
        //{
        //    XmlSerializer ser = new XmlSerializer(typeof(InstallerScript));
        //    ser.Serialize(stream, this);
        //}
        //
        //public byte[] Serialize()
        //{
        //    using (MemoryStream stream = new MemoryStream()) {
        //        Serialize(stream);
        //        return stream.GetBuffer();
        //    }
        //}
        //
        //#endregion
    }
}
