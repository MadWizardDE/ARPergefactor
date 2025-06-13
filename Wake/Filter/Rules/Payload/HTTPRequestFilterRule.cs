using System.Text;
using System.Text.RegularExpressions;

namespace MadWizard.ARPergefactor.Wake.Filter.Rules
{
    internal class HTTPRequestFilterRule : PayloadFilterRule
    {
        public required string? Method { get; init; }
        public required string? Path { get; init; }
        public required string? Version { get; init; }
        public required string? Host { get; init; }

        public Dictionary<string, string?> Header { get; init; } = [];
        public Dictionary<string, string?> Cookie { get; init; } = [];

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
                throw new ArgumentException("Invalid HTTP request format"); // FIXME HTTPRequestFilter still hast to implemented
            }
        }
    }
}
