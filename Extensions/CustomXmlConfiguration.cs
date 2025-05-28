using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Extensions.Configuration.Xml
{
    internal class CustomXmlConfigurationSource : XmlConfigurationSource
    {
        internal readonly List<string> EnumAttributes = [];

        public CustomXmlConfigurationSource(string path, bool optional, bool reloadOnChange)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException($"path = {path}");

            Path = path;
            Optional = optional;
            ReloadOnChange = reloadOnChange;

            ResolveFileProvider();
        }

        public CustomXmlConfigurationSource AddEnumAttribute(string name)
        {
            EnumAttributes.Add(name);
            return this;
        }

        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);

            return new CustomXmlConfigurationProvider(this);
        }

    }

    partial class CustomXmlConfigurationProvider(CustomXmlConfigurationSource source) : XmlConfigurationProvider(source)
    {
        internal const string EMPTY_ATTRIBUTE_NAME = "__empty";
        internal const string TEXT_ATTRIBUTE_NAME = "text";

        internal static Regex TimeSpanRegex = TimeSpanPattern();

        public override void Load(Stream stream)
        {
            using MemoryStream memory = new();

            XDocument xml = XDocument.Load(stream);
            TraverseNodes(xml.Root!);
            xml.Save(memory);

            memory.Position = 0;

            base.Load(memory);
        }

        private void TraverseNodes(XElement element)
        {
            SupportNameslessNodes(element);

            SupportTextNode(element);
            SupportEmptyNode(element);

            SupportTimeSpanAttribute(element);
            SupportEnumAttributes(element);

            foreach (XElement childElement in element.Elements())
                TraverseNodes(childElement);
        }

        private void SupportEnumAttributes(XElement element)
        {
            foreach (var attribute in element.Attributes())
            {
                if (source.EnumAttributes.Contains(attribute.Name.LocalName))
                {
                    attribute.Value = attribute.Value.Replace("|", ",").Trim();
                }
            }
        }

        private static void SupportNameslessNodes(XElement element)
        {
            // TODO how can we fix this?
        }

        private static void SupportTextNode(XElement element)
        {
            var text = string.Concat(element.Nodes().OfType<XText>().Select(t => t.Value));

            if (!string.IsNullOrWhiteSpace(text))
            {
                element.Add(new XAttribute(TEXT_ATTRIBUTE_NAME, text)); // make text content accessible
            }
        }

        private static void SupportEmptyNode(XElement element)
        {
            if (!(element.HasAttributes || element.HasElements))
            {
                element.Add(new XAttribute(EMPTY_ATTRIBUTE_NAME, "true")); // allow empty nodes
            }
        }

        private static void SupportTimeSpanAttribute(XElement element)
        {
            foreach (var attribute in element.Attributes())
            {
                if (ISO8601TimeSpanPattern().Match(attribute.Value).Success)
                {
                    TimeSpan time = XmlConvert.ToTimeSpan(attribute.Value);

                    attribute.Value = time.ToString();
                }

                else if (TimeSpanPattern().Match(attribute.Value) is Match match && match.Success)
                {
                    TimeSpan time = TimeSpan.Zero;
                    if (match.Groups.TryGetValue("hours", out var hours) && hours.Success)
                        time += TimeSpan.FromHours(int.Parse(hours.Value));
                    if (match.Groups.TryGetValue("minutes", out var minutes) && minutes.Success)
                        time += TimeSpan.FromMinutes(int.Parse(minutes.Value));
                    if (match.Groups.TryGetValue("seconds", out var seconds) && seconds.Success)
                        time += TimeSpan.FromSeconds(int.Parse(seconds.Value));
                    if (match.Groups.TryGetValue("milliseconds", out var milliseconds) && milliseconds.Success)
                        time += TimeSpan.FromMilliseconds(int.Parse(milliseconds.Value));

                    attribute.Value = time.ToString();
                }
            }
        }

        [GeneratedRegex(@"^P(?=\d|T\d)(\d+Y)?(\d+M)?(\d+D)?(T(\d+H)?(\d+M)?(\d+S)?)?$")]
        private static partial Regex ISO8601TimeSpanPattern();


        [GeneratedRegex(@"^(?=.*\d+(?:h|min|s|ms))(?:(?<hours>\d+)h)?(?:(?<minutes>\d+)min)?(?:(?<seconds>\d+)s)?(?:(?<milliseconds>\d+)ms)?$")]
        private static partial Regex TimeSpanPattern();
    }
}