using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Xml;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Extensions.Configuration.Xml
{
    class CustomXmlConfigurationSource : XmlConfigurationSource
    {
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);
            return new CustomXmlConfigurationProvider(this);
        }
    }
}
