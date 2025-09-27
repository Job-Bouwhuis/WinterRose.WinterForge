namespace WinterRose.WinterForgeSerializing.Workers
{
    internal class ScopePusher : IDisposable
    {
        private readonly InstructionExecutor executor;
        private bool disposed = false;
        public ScopePusher(InstructionExecutor executor)
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
