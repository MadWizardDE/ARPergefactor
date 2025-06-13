namespace MadWizard.ARPergefactor.Wake.Filter.Rules
{
    public abstract class FilterRule
    {
        public required FilterRuleType Type { get; set; }

        public virtual bool ShouldWhitelist => Type == FilterRuleType.Must;
        public virtual bool ShouldBlacklist => Type == FilterRuleType.MustNot;
    }

    public enum FilterRuleType
    {
        Must,
        MustNot
    }
}
