using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;

namespace AppInstall.Framework
{
    /// <summary>
    /// Config provider that loads and stores configuration data in a structured way based on XML.
    /// All except Config and Load operations are thread-safe.
    /// </summary>
    public class Config
    {
        private static Config commonConfig = new Config(ApplicationControl.CommonConfigPath);
        private static Config userConfig = new Config(ApplicationControl.UserConfigPath);

        public static Config CommonConfig { get { return commonConfig; } }
        public static Config UserConfig { get { return userConfig; } }


        string filePath;
        object lockRef = new object();
        XmlDocument xml;

        public bool IsLoaded { get { lock (lockRef) return xml != null; } }

        /// <summary>
        /// Triggered before the config data is being saved to non-volatile memory.
        /// Any item that should be saved can use this event to be notified.
        /// </summary>
        public event Action<Config> WillSave;


        /// <summary>
        /// Creates a new configuration object associated with the specified XML file.
        /// </summary>
        /// <param name="mustExist">if set to false, the file will be created automatically</param>
        public Config(string path, bool mustExist)
        {
            if (mustExist)
                if (!File.Exists(path))
                    throw new FileNotFoundException(Path.GetFullPath(path));
            filePath = path;
        }

        /// <summary>
        /// Creates a new configuration object associated with the specified XML file.
        /// If the file doesn't exist it will be created on Load.
        /// Hence the application should have write permission at the specified location.
        /// </summary>
        public Config(string path)
            : this(path, false)
        {
        }


        /// <summary>
        /// Reloads the configuration data from non-volatile memory.
        /// Loading prior to usage is not explicitly required.
        /// </summary>
        public void Reload()
        {
            lock (lockRef) {
                xml = new XmlDocument();
                if (File.Exists(filePath))
                    xml.Load(filePath);
                else
                    xml.LoadXml("<config></config>");
            }
        }



        /// <summary>
        /// Stores the configuration data in permanent memory after raising the WillSave event.
        /// Any changes made to the configuration won't be retained unless this routine is called.
        /// </summary>
        public void Save()
        {
            lock (lockRef) {
                if (!IsLoaded)
                    return;
                WillSave.SafeInvoke(this);
                Utilities.CreateDirectory(Directory.GetParent(filePath).FullName);
                lock (xml)
                    xml.Save(filePath);
            }
        }


        /// <summary>
        /// Returns or sets the configuration item with the specified path.
        /// Returns null if the item does not exist or is empty.
        /// Make sure to load the configuration first.
        /// </summary>
        /// <param name="path">The path is of the format: group/subgroup/item#some_id</param>
        public string this[string path]
        {
            get
            {
                lock (lockRef) {
                    if (!IsLoaded) Reload();
                    string result = GetNode(path).InnerXml;
                    return (string.IsNullOrEmpty(result) ? null : result);
                }
            }
            set
            {
                lock (lockRef) {
                    if (!IsLoaded) Reload();
                    GetNode(path).InnerXml = value;
                }
            }
        }

        /// <summary>
        /// Returns the path of every node in the specified path
        /// </summary>
        public IEnumerable<string> ChildNodes(string path)
        {
            lock (lockRef) {
                if (!IsLoaded) Reload();
                foreach (XmlNode node in GetNode(path).ChildNodes) {
                    string nodePath = path + "/" + node.Name;
                    if (node.Attributes["id"] == null)
                        yield return nodePath;
                    yield return nodePath + "#" + node.Attributes["id"].Value;
                }
            }
            yield break;
        }

        /// <summary>
        /// Executes the specified action on all subnodes in the specified path passing the full node path as an argument.
        /// </summary>
        public void ForAllChildNodes(string path, Action<string> action)
        {
            foreach (string node in ChildNodes(path))
                action(node);
        }


        /// <summary>
        /// Returns the node at the specified path. If the node doesn't exist it will be created first.
        /// A path like "node/subnode/item#42" addresses the "item" node in "node/subnode" that has an "id"-attribute of "42". Non-integer IDs are allowed. Empty path elements are ignored.
        /// This function must not be called outside a lock.
        /// </summary>
        private XmlNode GetNode(string path)
        {
            XmlNode supernode;
            XmlNode node = xml.DocumentElement;
            string name, id;

            foreach (string pathElement in path.Split('/').Where((s) => !string.IsNullOrEmpty(s))) {
                supernode = node;
                node = null;
                id = null;

                if (pathElement.Contains("#")) {
                    name = pathElement.Substring(0, pathElement.IndexOf("#"));
                    id = pathElement.Substring(pathElement.IndexOf("#") + 1);

                    foreach (XmlNode n in supernode.SelectNodes(name)) {
                        XmlAttribute a = n.Attributes["id"];
                        if (a != null)
                            if (a.Value == id) {
                                node = n;
                                break;
                            }
                    }
                } else {
                    name = pathElement;
                    node = supernode.SelectSingleNode(name);
                }

                if (node == null) {
                    node = supernode.AppendChild(xml.CreateElement(name));
                    if (id != null) node.Attributes.Append(xml.CreateAttribute("id")).Value = id;
                }
            }
            return node;
        }
    }
}
