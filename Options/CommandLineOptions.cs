using CommandLine;

namespace MadWizard.ARPergefactor.Options
{
    internal class CommandLineOptions
    {
        [Option('a', "auto-reload", Required = false, Default = false, HelpText = "Enable automatic reloading after config change.")]
        public bool AutoReload { get; set; }

        [Option('p', "auto-reload-path", Required = false, HelpText = "Enable automatic reloading after config change (in a different directory).")]
        public string? AutoReloadPath { get; set; }

    }
}
