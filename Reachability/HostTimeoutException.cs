namespace MadWizard.ARPergefactor.Reachability
{
    public class HostTimeoutException(TimeSpan timeout) : TimeoutException
    {
        public TimeSpan Timeout => timeout;
    }
}
