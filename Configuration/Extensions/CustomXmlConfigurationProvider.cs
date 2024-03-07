using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Extensions.Configuration.Xml
{
    class CustomXmlConfigurationProvider(XmlConfigurationSource source) : XmlConfigurationProvider(source)
    {
        internal const string EMPTY_ATTRIBUTE_NAME = "__empty";
        internal const string TEXT_ATTRIBUTE_NAME = "text";

        public override void Load(Stream stream)
        {
            using MemoryStream memory = new();

            XDocument xml = XDocument.Load(stream);
            PopulateEmptyNodes(xml.Root!);
            xml.Save(memory);

            memory.Position = 0;

            base.Load(memory);
        }

        private static void PopulateEmptyNodes(XElement element)
        {
            var empty = true;
            if (element.Attributes().Count() > 0)
                empty = false;
            else if (element.Elements().Count() > 0)
                empty = false;

            var text = string.Concat(element.Nodes().OfType<XText>().Select(t => t.Value));

            if (!string.IsNullOrWhiteSpace(text))
            {
                element.Add(new XAttribute(TEXT_ATTRIBUTE_NAME, text));
            }

            if (empty)
            {
                element.Add(new XAttribute(EMPTY_ATTRIBUTE_NAME, "true"));
            }
            else
            {
                foreach (XElement childElement in element.Elements())
                    PopulateEmptyNodes(childElement);
            }
        }
    }
}