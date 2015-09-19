using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Linq;
using System.Data.Linq.Mapping;

namespace AppInstall.Framework
{

    public class LogDBDataContext : AutoSubmitDataContext
    {
        public Table<LogDB.Instance> Instances { get { return GetTable<LogDB.Instance>(); } }
        public Table<LogDB.LogEntry> LogEntries { get { return GetTable<LogDB.LogEntry>(); } }
    }

    /// <summary>
    /// Hooks into a log context to log all messages in a database.
    /// The connected SQL server must have a database called "System", which must contain
    /// the "Log" and "Instances" table as seen on the AppInstall Enterprise Network.
    /// </summary>
    public class LogDB : Database<LogDBDataContext>
    {

        #region "DB Abstraction Classes"


        [Table(Name = "Instances")]
        public class Instance
        {
            [Column(IsPrimaryKey = true, CanBeNull = false, IsDbGenerated = true)]
            public Guid Guid { get; private set; }

            [Column(CanBeNull = false)]
            public string Application { get; set; }

            [Column(CanBeNull = false)]
            public Guid Version { get; set; }

            [Column(CanBeNull = false)]
            public byte[] Machine { get; set; }
        }

        [Table(Name = "Log")]
        public class LogEntry
        {
            [Column(IsPrimaryKey = true, CanBeNull = false, IsDbGenerated = true)]
            public long ID { get; private set; }

            [Column(CanBeNull = false)]
            public Guid Instance { get; set; }

            [Column(CanBeNull = false)]
            public DateTime Time { get; set; }

            [Column(CanBeNull = false)]
            public string Context { get; set; }

            [Column(CanBeNull = false)]
            public string Message { get; set; }

            [Column(CanBeNull = false)]
            public LogType Type { get; set; }
        }


        #endregion


        public override string TableName { get { return "System"; } }



        /// <summary>
        /// Returns the current instance.
        /// An instance uniquely identifies an application, its version and the machine it is running on.
        /// </summary>
        public Instance GetCurrentInstance()
        {
            string app = Application.ApplicationName;
            byte[] machine = Utilities.GetMachineID();
            Guid version;

            try {
                version = Installer.InstallerSystem.GetPackageID();
            } catch (Exception ex) {
                logContext.Log(ex.Message, LogType.Warning);
                version = new Guid();
            }

            using (var db = OpenContext()) {
                // retrieve existing instance or create new
                Instance instance = (from i in db.Instances where (i.Application == app) && (i.Machine == machine) && (i.Version == version) select i).SingleOrDefault();
                if (instance == null) {
                    db.Instances.InsertOnSubmit(instance = new Instance() {
                        Application = app,
                        Machine = machine,
                        Version = version
                    });
                    logContext.Log("created new instance for DB log");
                }

                return instance;
            }
        }


        /// <summary>
        /// After calling this function, all logging activity on the specified log context is recorded in the database.
        /// The Guid of this instance is returned.
        /// </summary>
        public Guid StartLogging(LogContext source)
        {
            Guid instance = GetCurrentInstance().Guid;

            source.Hook((logDelegate) => (context, message, type) => {

                // first log using the original delegate
                logDelegate(context, message, type);

                // then try logging in the database
                try {
                    using (var db = OpenContext())
                        db.LogEntries.InsertOnSubmit(new LogEntry() {
                            Instance = instance,
                            Time = DateTime.UtcNow,
                            Context = context,
                            Message = message,
                            Type = type
                        });
                } catch (Exception ex) {
                    logDelegate(context + "->db log", "logging in the database failed: " + ex.Message, LogType.Warning);
                }
            });

            return instance;
        }

    }
}
