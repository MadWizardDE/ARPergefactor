namespace MadWizard.ARPergefactor.Wake.Filter.Rules
{
    public abstract class PayloadFilterRule : FilterRule
    {
        public abstract bool Matches(byte[] data);
    }
}
