
// external references:
// System.Data
// System.Data.Linq

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Linq;
using System.Linq.Expressions;
using AppInstall.Organization;

namespace AppInstall.Framework
{
    public class AutoSubmitDataContext : IDisposable
    {
        public DataContext DB { get; set; }

        public Table<T> GetTable<T>()
            where T : class
        {
            return DB.GetTable<T>();
        }

        public void InsertAndSubmit<T>(Table<T> table, T entity)
            where T : class
        {
            table.InsertOnSubmit(entity);
            DB.SubmitChanges();
        }

        public void InsertAndSubmit<T>(Table<T> table, IEnumerable<T> entities)
            where T : class
        {
            table.InsertAllOnSubmit(entities);
            DB.SubmitChanges();
        }

        /// <summary>
        /// Discards all changes.
        /// </summary>
        public void Discard()
        {
            DB.Dispose();
            DB = null;
        }

        /// <summary>
        /// Submits all changes.
        /// </summary>
        public void Dispose()
        {
            if (DB != null)
                try {
                    DB.SubmitChanges();
                } finally {
                    Discard();
                }
        }
    }


    public abstract class Database<T>
        where T : AutoSubmitDataContext, new()
    {

        private const string CONNECTION_DETAILS = "Initial Catalog={0};Integrated Security=False;Application Name={1};Connection Timeout=5;User ID={2};Password={3}";
        private const int CONNECTION_ATTEMPT_INTERVAL = 10000;

        //protected object dbLock = new object();
        private string dataSource;

        protected LogContext logContext { get; private set; }

        public abstract string TableName { get; }

        public bool IsConnected { get { return dataSource != null; } }

        /// <summary>
        /// Blocks until either a connection to the database could be established or the operation was cancelled.
        /// </summary>
        /// <exception cref="OperationCancelledException">The operation was cancelled</exception>
        public void Setup(string userID, string password, LogContext logContext, CancellationToken cancellationToken, params string[] servers)
        {
            if (!servers.Any()) throw new ArgumentException("no servers specified");

            string connectionDetails = string.Format(CONNECTION_DETAILS, TableName, Application.ApplicationName, userID, password); // todo: mask arguments
            this.logContext = logContext;

            Exception[] errors = new Exception[servers.Count()];
            for (int i = 0; i < servers.Count(); i++) {
                string probeDataSource = "Data Source=" + servers[i] + ";" + connectionDetails;
                try {
                    using (var db = new DataContext(probeDataSource))
                        if (!db.DatabaseExists())
                            throw new Exception("the database was not found");
                    logContext.Log("connected to " + TableName + " on " + servers[i], LogType.Info);
                    dataSource = probeDataSource;
                    return;
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception ex) {
                    errors[i] = ex;
                }
            }


            logContext.Log("could not connect to database " + TableName + " on any server", LogType.Error);
            for (int i = 0; i < servers.Count(); i++)
                logContext.Log("  failed to connect to " + servers[i] + ": " + errors[i].Message, LogType.Warning);

            throw new AggregateException("none of the databases could be reached", errors);
        }

        protected T OpenContext()
        {
            if (!IsConnected)
                throw new InvalidOperationException("the database connection was not setup");
            return new T() { DB = new DataContext(dataSource) };
        }
    }

    public static class DBExtensions
    {

        public static T SelectByPK<T, K>(this Table<T> table, K key) where T : class
        {
            var pkColumn = table.Context.Mapping.GetTable(typeof(T)).RowType.DataMembers.SingleOrDefault((d) => d.IsPrimaryKey);
            if (pkColumn == null)
                throw new Exception("the table does not contain a primary key column");
            var param = Expression.Parameter(typeof(T), "e");
            return table.SingleOrDefault(Expression.Lambda<Func<T, bool>>(Expression.Equal(Expression.Property(param, pkColumn.Name), Expression.Constant(key)), param));
        }

        public static void RefreshAll<T>(this T[] elements, DataContext context, RefreshMode mode)
        {
            foreach (T element in elements)
                context.Refresh(RefreshMode.OverwriteCurrentValues, element);
        }
    }
}
