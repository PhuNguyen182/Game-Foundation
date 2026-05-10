namespace DracoRuan.Foundation.DataFlow.DataMigration.Core.Orchestrator
{
    public enum RollbackStrategy
    {
        None = 0,
        DomainOnly = 1,
        Fully = 2,
    }
}