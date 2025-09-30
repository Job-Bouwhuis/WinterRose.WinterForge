namespace WinterRose.WinterForgeSerializing.Workers
{
    internal class ScopePusher : IDisposable
    {
        private readonly WinterForgeVM executor;
        private bool disposed = false;
        public ScopePusher(WinterForgeVM executor)
        {
            this.executor = executor;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (executor.scopeStack.Count > 0)
                executor.scopeStack.Pop();
        }
    }


}
