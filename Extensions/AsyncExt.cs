namespace MadWizard.ARPergefactor.Extensions
{
    internal static class SemaphoreExt
    {
        public static IDisposable Use(this SemaphoreSlim semaphore)
        {
            semaphore.Wait();

            return new SemaphoreLease(semaphore);
        }

        private class SemaphoreLease(SemaphoreSlim semaphore) : IDisposable
        {
            public void Dispose() => semaphore.Release();
        }
    }
}
