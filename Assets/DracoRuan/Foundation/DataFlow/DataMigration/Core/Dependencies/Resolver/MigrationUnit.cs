using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.DataMigration.Migrator;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Core.Dependencies.Resolver
{
    public class MigrationUnit
    {
        public string Domain { get; }
        public List<IDataMigrator> MigratorChain { get; }
        public IReadOnlyList<string> Dependencies { get; private set; }

        public MigrationUnit(string domain, List<IDataMigrator> migratorChain)
        {
            this.Domain = domain;
            this.MigratorChain = migratorChain;
            UnionAllDependencies();
            return;

            void UnionAllDependencies()
            {
                HashSet<string> dependencies = new();
                foreach (IDataMigrator migrator in this.MigratorChain)
                {
                    foreach (string dependency in migrator.Dependencies)
                    {
                        if (string.CompareOrdinal(dependency, this.Domain) != 0)
                            dependencies.Add(dependency);
                    }
                }
                
                this.Dependencies = new List<string>(dependencies);
            }
        }
    }
}