using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AppInstall.Framework;

namespace AppInstall.Networking
{
    public static class Cookies
    {
        /// <summary>
        /// Parses the cookies from a message.
        /// </summary>
        public static Dictionary<string, string> FetchCookies<M, S>(NetMessage<M, S> message, bool isRequest)
            where M : struct, IConvertible
            where S : struct, IConvertible
        {
            return (from c in message.GetFieldOrDefault((isRequest ? "" : "Set-") + "Cookie", "").Split(';') where c.Contains('=') select c.Split('=')).ToDictionary((c) => c[0].Trim(), (c) => c[1].Trim());
        }

        /// <summary>
        /// Places a cookie on the message.
        /// The cookie must not contain any of the folloring chars: ;, =, \n
        /// </summary>
        public static void PlaceCookie<M, S>(NetMessage<M, S> message, string name, string content, bool isRequest)
            where M : struct, IConvertible
            where S : struct, IConvertible
        {
            string cookieField = (isRequest ? "" : "Set-") + "Cookie";
            string cookies = message.GetFieldOrDefault(cookieField, "");
            message[cookieField] = cookies + (cookies == "" ? "" : "; ") + name + "=" + content;
        }
    }
}
