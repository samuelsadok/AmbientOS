using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AppInstall.Framework;
using AppInstall.Networking;
using AppInstall.Organization;
using AppInstall.OS;
using AppInstall.Installer;

namespace AppInstall.Installer
{
    public class SoftwareDistributionClient
    {
        Client<HTTP.Methods, HTTP.StatusCodes> client;

        public SoftwareDistributionClient(LogContext logContext)
        {
            client = new Client<HTTP.Methods, HTTP.StatusCodes>(logContext) {
                Host = GlobalConstants.SOFTWARE_PROVIDER_SERVER,
                Port = GlobalConstants.SOFTWARE_PROVIDER_SERVER_PORT,
                ResponseCheck = (request, response) => {
                    if (response.StatusCode == HTTP.StatusCodes.OK)
                        return null;
                    return new Exception("server returned status code \"" + response.Header + "\"");
                }
            };
        }

        /// <summary>
        /// Queries the software distribution server to check for an update and returns null or the script that describes the update.
        /// </summary>
        /// <param name="packageID">The identifier of the package for which an update should be found</param>
        /// <param name="channel">The name of the channel that should be checked for updates (such as "beta" or "main")</param>
        public async Task<InstallerScript> GetUpdateScript(Guid packageID, string channel, CancellationToken cancellationToken)
        {
            var request = new NetMessage<HTTP.Methods, HTTP.StatusCodes>(SoftwareDistributionProtocol.PROTOCOL_IDENTIFIER, HTTP.Methods.GET,
                SoftwareDistributionProtocol.UPDATE_SCRIPT_RESOURCE + "/" + packageID.ToString(),
                new Dictionary<string, string>() { { "channel", channel } }
                );
            return Utilities.XMLDeserialize<InstallerScript>(((BinaryContent)(await client.SendRequest(request, cancellationToken)).Content).Content);
        }

        /// <summary>
        /// Queries the software distribution server to retrieve the installation script for the specified application
        /// </summary>
        public async Task<InstallerScript> GetInstallScript(string application, string channel, string platform, CancellationToken cancellationToken)
        {
            var request = new NetMessage<HTTP.Methods, HTTP.StatusCodes>(SoftwareDistributionProtocol.PROTOCOL_IDENTIFIER, HTTP.Methods.GET,
                SoftwareDistributionProtocol.APPLICATION_RESOURCE + "/" + application.EscapeForURL(),
                new Dictionary<string, string>() { { "channel", channel }, { "platform", platform } }
                );
            return Utilities.XMLDeserialize<InstallerScript>(((BinaryContent)(await client.SendRequest(request, cancellationToken)).Content).Content);
        }

        /// <summary>
        /// Retrieves the file with the specified hash code from the software distribution server.
        /// </summary>
        public async Task<byte[]> DownloadFile(Guid guid, CancellationToken cancellationToken)
        {
            var request = new NetMessage<HTTP.Methods, HTTP.StatusCodes>(SoftwareDistributionProtocol.PROTOCOL_IDENTIFIER, HTTP.Methods.GET, SoftwareDistributionProtocol.FILE_RESOURCE + "/" + guid);
            return ((BinaryContent)((await client.SendRequest(request, cancellationToken)).Content)).Content;
        }
    }
}
