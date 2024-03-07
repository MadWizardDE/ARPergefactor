using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Extensions.Configuration.Xml
{
    static class CustomXmlConfigurationExtensions
    {
        public static IConfigurationBuilder AddCustomXmlFile(this IConfigurationBuilder builder, string path, bool optional = false, bool reloadOnChange = false)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException($"path = {path}");

            var source = new CustomXmlConfigurationSource
            {
                FileProvider = null,
                Path = path,
                Optional = optional,
                ReloadOnChange = reloadOnChange,
            };

            source.ResolveFileProvider();

            return builder.Add(source);
        }
    }
}