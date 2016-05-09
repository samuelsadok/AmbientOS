using EnvDTE;
using System;
using System.Runtime.CompilerServices;

namespace AmbientOS.VisualStudio
{
    public class VSCommandInterceptor : IDisposable
    {
        private bool isDisposed;

        private CommandEvents commandEvents;
        
        public event EventHandler<EventArgs> AfterExecute;
        public event EventHandler<EventArgs> BeforeExecute;

        private VSCommandInterceptor(DTE dte, Guid commandGuid, int commandId)
        {
            if (dte != null) {
                commandEvents = dte.Events.get_CommandEvents(commandGuid.ToString("B"), commandId);

                if (commandEvents != null) {
                    commandEvents.AfterExecute += OnAfterExecute; //new _dispCommandEvents_AfterExecuteEventHandler(this, *(UIntPtr)ldftn(OnAfterExecute));
                    commandEvents.BeforeExecute += OnBeforeExecute; // new _dispCommandEvents_BeforeExecuteEventHandler(this, (UIntPtr)ldftn(OnBeforeExecute));
                }
            }
        }

        public static VSCommandInterceptor FromEnum<TEnum>(IServiceProvider serviceProvider, TEnum command)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException($"{serviceProvider}");

            var dte = serviceProvider.GetService(typeof(DTE)) as DTE;
            return FromEnum(dte, command);
        }

        public static VSCommandInterceptor FromEnum<TEnum>(DTE dte, TEnum command)
        {
            return new VSCommandInterceptor(dte, typeof(TEnum).GUID, (int)((object)command));
        }

        private void OnAfterExecute(string Guid, int ID, object CustomIn, object CustomOut)
        {
            if (AfterExecute != null)
                AfterExecute(this, new EventArgs());
        }

        private void OnBeforeExecute(string Guid, int ID, object CustomIn, object CustomOut, ref bool CancelDefault)
        {
            if (BeforeExecute != null)
                BeforeExecute(this, new EventArgs());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!isDisposed & disposing) {
                if (commandEvents != null) {
                    commandEvents.AfterExecute -= OnAfterExecute;
                    commandEvents.BeforeExecute -= OnBeforeExecute;
                }
                isDisposed = true;
            }
        }
    }
}
