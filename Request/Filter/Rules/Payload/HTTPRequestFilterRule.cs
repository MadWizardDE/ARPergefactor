using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MadWizard.ARPergefactor.Request.Filter.Rules.Payload
{
    internal class HTTPRequestFilterRule : PayloadFilterRule
    {
        public required string? Path { get; init; }

        public override bool Matches(byte[] data)
        {
            var http = new HTTPRequest(data);

            if (Path != null && http.Target.Contains(Path))
            {
                return true;
            }

            return false;
        }
    }

    file class HTTPRequest
    {
        public string Method { get; init; }

        public string Target { get; init; }

        public string Version { get; init; }

        internal HTTPRequest(byte[] payload)
        {
            string text = Encoding.Default.GetString(payload);
            string[] lines = Regex.Split(text, "\r\n|\r|\n");
            string first = lines[0];
            string[] parts = first.Split(' ');

            if (parts.Length > 2)
            {
                Method = parts[0];
                Target = parts[1];
                Version = parts[2];
            }
            else
            {
                throw new ArgumentException("Invalid HTTP request format"); // TODO testen
            }
        }
    }
}
