using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.Installer
{
    public class SoftwareDBDataContext : AutoSubmitDataContext
    {
        public Table<SoftwareDB.Application> Applications { get { return GetTable<SoftwareDB.Application>(); } }
        public Table<SoftwareDB.Channel> Channels { get { return GetTable<SoftwareDB.Channel>(); } }
        public Table<SoftwareDB.Platform> Platforms { get { return GetTable<SoftwareDB.Platform>(); } }
        public Table<SoftwareDB.File> Files { get { return GetTable<SoftwareDB.File>(); } }
        public Table<SoftwareDB.Package> Packages { get { return GetTable<SoftwareDB.Package>(); } }
        public Table<SoftwareDB.PackageContent> PackageContents { get { return GetTable<SoftwareDB.PackageContent>(); } }
        public Table<SoftwareDB.LatestPackage> LatestPackages { get { return GetTable<SoftwareDB.LatestPackage>(); } }
    }

    public class SoftwareDB : Database<SoftwareDBDataContext>
    {
        private const string DEFAULT_CHANNEL = "release";

        #region "DB Abstraction Classes"

        [Table(Name = "Applications")]
        public class Application
        {
            [Column(IsPrimaryKey = true, CanBeNull = false, IsDbGenerated = true)]
            public Guid Guid { get; private set; }

            [Column(CanBeNull = false)]
            public string Name { get; set; }

            [Column(CanBeNull = false)]
            public string Description { get; set; }
        }

        [Table(Name = "Channels")]
        public class Channel
        {
            [Column(IsPrimaryKey = true, CanBeNull = false, IsDbGenerated = true)]
            public Guid Guid { get; private set; }

            [Column(CanBeNull = false)]
            public string Name { get; set; }
        }

        [Table(Name = "Platforms")]
        public class Platform
        {
            [Column(IsPrimaryKey = true, CanBeNull = false, IsDbGenerated = true)]
            public Guid Guid { get; private set; }

            [Column(CanBeNull = false)]
            public string Name { get; set; }

            [Association(ThisKey = "installerGuid", Storage = "installer", IsForeignKey = true)]
            public File Installer { get { return installer.Entity; } set { installer.Entity = value; } }
            private EntityRef<File> installer = new EntityRef<File>();
            [Column(Name = "installer", CanBeNull = false)]
            protected Guid installerGuid;
        }

        [Table(Name = "Files")]
        public class File
        {
            [Column(IsPrimaryKey = true, CanBeNull = false, IsDbGenerated = true)]
            public Guid Guid { get; private set; }

            [Column(CanBeNull = false)]
            public byte[] Hash { get; set; }

            [Column(CanBeNull = false)]
            public byte[] Content { get; set; }
        }

        [Table(Name = "Packages")]
        public class Package
        {
            [Column(IsPrimaryKey = true, CanBeNull = false, IsDbGenerated = true)]
            public Guid Guid { get; private set; }

            [Association(ThisKey = "successorGuid", Storage = "successor", IsForeignKey = true)]
            public Package Successor { get { return successor.Entity; } set { successor.Entity = value; } }
            private EntityRef<Package> successor = new EntityRef<Package>();
            [Column(Name = "successor", CanBeNull = true)]
            protected Guid? successorGuid;

            [Column(CanBeNull = false)]
            public bool IsHard { get; set; }

            [Association(OtherKey = "packageGuid", Storage = "content", IsForeignKey = true)]
            public EntitySet<PackageContent> Content { get { return content; } set { content.Assign(value); } }
            private EntitySet<PackageContent> content = new EntitySet<PackageContent>();
        }

        [Table(Name = "PackageContents")]
        public class PackageContent
        {
            [Column(IsPrimaryKey = true, CanBeNull = false, IsDbGenerated = true)]
            public Guid Guid { get; private set; }

            [Association(ThisKey = "packageGuid", Storage = "package", IsForeignKey = true)]
            public Package Package { get { return package.Entity; } set { package.Entity = value; } }
            private EntityRef<Package> package = new EntityRef<Package>();
            [Column(Name = "package", CanBeNull = false)]
            protected Guid packageGuid;

            [Association(ThisKey = "fileGuid", Storage = "file", IsForeignKey = true)]
            public File File { get { return file.Entity; } set { file.Entity = value; } }
            private EntityRef<File> file = new EntityRef<File>();
            [Column(Name = "file", CanBeNull = false)]
            protected Guid fileGuid;

            [Column(CanBeNull = false)]
            public int PathRoot { get; set; }

            [Column(CanBeNull = false)]
            public string Path { get; set; }

            public override bool Equals(object obj)
            {
                PackageContent o = obj as PackageContent;
                if (o == null) return false;
                return File == o.File && (PathRoot == o.PathRoot) && (Path == o.Path);
            }

            public override int GetHashCode()
            {
                return File.GetHashCode() ^ PathRoot.GetHashCode() ^ Path.GetHashCode();
            }
        }

        [Table(Name = "LatestPackages")]
        public class LatestPackage
        {
            [Column(IsPrimaryKey = true, CanBeNull = false, IsDbGenerated = true)]
            public Guid Guid { get; private set; }

            [Association(ThisKey = "channelGuid", Storage = "channel", IsForeignKey = true)]
            public Channel Channel { get { return channel.Entity; } set { channel.Entity = value; } }
            private EntityRef<Channel> channel = new EntityRef<Channel>();
            [Column(Name = "channel", CanBeNull = false)]
            protected Guid channelGuid;

            [Association(ThisKey = "platformGuid", Storage = "platform", IsForeignKey = true)]
            public Platform Platform { get { return platform.Entity; } set { platform.Entity = value; } }
            private EntityRef<Platform> platform = new EntityRef<Platform>();
            [Column(Name = "platform", CanBeNull = false)]
            protected Guid platformGuid;

            [Association(ThisKey = "applicationGuid", Storage = "application", IsForeignKey = true)]
            public Application Application { get { return application.Entity; } set { application.Entity = value; } }
            private EntityRef<Application> application = new EntityRef<Application>();
            [Column(Name = "app", CanBeNull = false)]
            protected Guid applicationGuid;

            [Association(ThisKey = "packageGuid", Storage = "package", IsForeignKey = true)]
            public Package Package { get { return package.Entity; } set { package.Entity = value; } }
            private EntityRef<Package> package = new EntityRef<Package>();
            [Column(Name = "package", CanBeNull = false)]
            protected Guid packageGuid;
        }

        #endregion

        private System.Security.Cryptography.SHA256CryptoServiceProvider fileHashFunction = new System.Security.Cryptography.SHA256CryptoServiceProvider();

        public override string TableName { get { return "Software"; } }


        /// <summary>
        /// Returns the content of the specified file.
        /// </summary>
        public byte[] GetFile(Guid guid)
        {
            using (var db = OpenContext())
                return db.Files.SelectByPK(guid).Content;
        }

        /// <summary>
        /// Checks if the specified file is stored in the database
        /// </summary>
        public bool FileExists(Guid guid)
        {
            using (var db = OpenContext())
                return (from file in db.Files where file.Guid == guid select file).Any();
        }

        /// <summary>
        /// Uploads a file to the database if it does not already exist
        /// </summary>
        public File UploadFile(byte[] content)
        {
            using (var db = OpenContext())
                return UploadFile(db, content);
        }

        /// <summary>
        /// Uploads a file to the database if it does not already exist
        /// </summary>
        private File UploadFile(SoftwareDBDataContext db, byte[] content)
        {
            var hash = fileHashFunction.ComputeHash(content);
            var file = (from f in db.Files where f.Hash == hash select f).SingleOrDefault();
            if (file == null) {
                logContext.Log("uploading file of length " + content.Count());
                file = new File() { Hash = hash, Content = content };
                db.Files.InsertOnSubmit(file);
            } else
                logContext.Log("skipping file of length " + content.Count());
            return file;
        }

        private void AddActionsToScript(InstallerScript script, IEnumerable<InstallerAction> actions)
        {
            foreach (var action in actions) {
                var opposite = script.Actions.SingleOrDefault((a) => action.HasOppositeEffect(a));
                if (opposite != null)
                    script.Actions.Remove(opposite);
                else
                    script.Actions.Add(action);
            }
        }

        /// <summary>
        /// Determines the difference between two packages and adds the according actions to the installer script.
        /// </summary>
        private void AddPackageToScript(SoftwareDBDataContext db, InstallerScript script, Guid package, Guid previousPackage)
        {
            var oldContent = (from file in db.PackageContents where file.Package.Guid == previousPackage select file);
            var newContent = (from file in db.PackageContents where file.Package.Guid == package select file);

            AddActionsToScript(script, (from file in oldContent.Except(newContent) select new InstallerDeleteFileAction { IsFolder = false, PathRoot = (InstallerFileAction.PathRootType)file.PathRoot, RelativePath = file.Path, Guid = file.File.Guid }));
            AddActionsToScript(script, (from file in newContent.Except(oldContent) select new InstallerInsertFileAction { IsFolder = false, PathRoot = (InstallerFileAction.PathRootType)file.PathRoot, RelativePath = file.Path, Guid = file.File.Guid, Overwrite = true }));
        }

        /// <summary>
        /// Determines the content of a package and adds the according actions to the installer script.
        /// </summary>
        private void AddPackageToScript(SoftwareDBDataContext db, InstallerScript script, Guid package)
        {
            var newContent = (from file in db.PackageContents where file.Package.Guid == package select file);
            script.Actions.AddRange(from file in newContent select new InstallerInsertFileAction { IsFolder = false, PathRoot = (InstallerFileAction.PathRootType)file.PathRoot, RelativePath = file.Path, Guid = file.File.Guid, Overwrite = true });
        }

        /// <summary>
        /// Determines if the application is initialized
        /// </summary>
        public bool IsInitialized(Guid application, string platform)
        {
            using (var db = OpenContext())
                return (from p in db.LatestPackages where p.Application.Guid == application && p.Platform.Name == platform select p).Any();
        }

        /// <summary>
        /// Generates the script for download and installation of the latest version of the specified application
        /// </summary>
        public InstallerScript GetInstallScript(string application, string platform, string channel)
        {
            if (channel == "default") channel = DEFAULT_CHANNEL;
            using (var db = OpenContext()) {
                var latestPackage = (from p in db.LatestPackages where p.Application.Name == application && p.Channel.Name == channel && p.Platform.Name == platform select p).Single();
                InstallerScript script = new InstallerScript() {
                    PackageID = latestPackage.Package.Guid,
                    UpdaterGuid = latestPackage.Platform.Installer.Guid,
                    Actions = new List<InstallerAction>()
                };
                AddPackageToScript(db, script, latestPackage.Package.Guid);
                return script;
            }
        }

        /// <summary>
        /// Generates the update script for the specified outdated package by checking on the specified channel
        /// </summary>
        public InstallerScript GetUpdateScript(Guid packageGuid, string channel)
        {
            if (channel == "default") channel = DEFAULT_CHANNEL;

            using (var db = OpenContext()) {
                LatestPackage latestPackage;
                var latestPackages = (from p in db.LatestPackages where p.Channel.Name == channel select p).ToArray();
                var package = db.Packages.SelectByPK(packageGuid);

                InstallerScript script = new InstallerScript() { Actions = new List<InstallerAction>() };

                if (!latestPackages.Any())
                    throw new Exception("the channel " + channel + " is empty");

                bool stopped = false;
                while ((latestPackage = latestPackages.SingleOrDefault((p) => p.Package.Guid == package.Guid)) == null) {
                    if (!stopped) {
                        logContext.Log("adding package to update");
                        AddPackageToScript(db, script, package.Successor.Guid, package.Guid);
                        script.PackageID = package.Successor.Guid;
                        if (package.Successor.IsHard) stopped = true;
                    }
                    package = package.Successor;
                }

                if (script.Actions.Count() == 0)
                    logContext.Log("empty update");

                if (script.Actions.Count() == 0)
                    return null;

                script.UpdaterGuid = latestPackage.Platform.Installer.Guid;
                return script;
            }
        }

        /// <summary>
        /// Creates a new publishing channel
        /// </summary>
        public Guid CreateChannel(string name)
        {
            Channel newChannel;
            using (var db = OpenContext()) {
                var query = db.Channels.Where((c) => c.Name == name);
                if (query.Any())
                    newChannel = query.Single();
                else
                    db.Channels.InsertOnSubmit(newChannel = new Channel() { Name = name });
            }
            return newChannel.Guid;
        }

        /// <summary>
        /// Uploads the installer binary for the specified platform and creates the platform if required. Returns the platform guid
        /// </summary>
        public Guid SetInstaller(string platformName, byte[] installer)
        {
            Platform platform;
            using (var db = OpenContext()) {
                var query = (from p in db.Platforms where p.Name == platformName select p);
                if (query.Any())
                    platform = query.Single();
                else
                    db.Platforms.InsertOnSubmit(platform = new Platform() { Name = platformName });
                platform.Installer = UploadFile(db, installer);
            }
            return platform.Guid;
        }

        /// <summary>
        /// Registers a new application and returns its guid.
        /// </summary>
        public Guid CreateApplication(string name, string description)
        {
            Application application;
            using (var db = OpenContext()) {
                var query = db.Applications.Where((app) => app.Name == name);
                if (query.Any()) throw new Exception("an application with the same name already exists");
                db.Applications.InsertOnSubmit(application = new Application() { Name = name, Description = description });
            }
            return application.Guid;
        }

        /// <summary>
        /// Creates a package from the files in specified the directory and uploads new files while ignoring files with any of the specified names.
        /// </summary>
        private Package UploadPackage(SoftwareDBDataContext db, string directory, IEnumerable<string> ignoreFiles)
        {
            var exclude = ignoreFiles.Select((f) => f.ToLower());
            Uri dir = new Uri(directory);

            logContext.Log("packing directory " + dir);

            Package result = new Package() { IsHard = false };

            db.Packages.InsertOnSubmit(result);

            foreach (string ignore in ignoreFiles)
                logContext.Log("ignoring " + ignore);

            db.PackageContents.InsertAllOnSubmit(from file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                                                 where !exclude.Contains(Path.GetFileName(file).ToLower())
                                                 select new PackageContent() {
                                                     Package = result,
                                                     File = UploadFile(db, System.IO.File.ReadAllBytes(file)),
                                                     PathRoot = (int)InstallerFileAction.PathRootType.ApplicationPath,
                                                     Path = dir.MakeRelativeUri(new Uri(file)).ToString()
                                                 });

            return result;
        }

        /// <summary>
        /// Publishes the specified package in the specified channel as an update for an old package
        /// </summary>
        public Package PublishPackage(string directory, IEnumerable<string> ignoreFiles, string channel, Guid predecessor)
        {
            using (var db = OpenContext()) {
                var package = UploadPackage(db, directory, ignoreFiles);
                var latestPackage = (from p in db.LatestPackages where p.Package.Guid == predecessor && p.Channel.Name == channel select p).Single();
                latestPackage.Package.Successor = package;
                latestPackage.Package = package;
                return package;
            }
        }

        /// <summary>
        /// Inits the specified application on all channels for the specified platform with the specified package
        /// </summary>
        public Package PublishPackage(string directory, IEnumerable<string> ignoreFiles, Guid application, string platformName)
        {
            using (var db = OpenContext()) {
                var package = UploadPackage(db, directory, ignoreFiles);
                foreach (var channel in db.Channels)
                    db.LatestPackages.InsertOnSubmit(new LatestPackage() {
                        Package = package,
                        Application = db.Applications.Single((a) => a.Guid == application),
                        Platform = db.Platforms.Single((p) => p.Name == platformName),
                        Channel = channel
                    });
                return package;
            }
        }
    }
}
