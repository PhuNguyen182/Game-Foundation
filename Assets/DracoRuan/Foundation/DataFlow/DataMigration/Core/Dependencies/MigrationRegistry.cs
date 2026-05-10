using System.Collections.Generic;
using System.Linq;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Exceptions;
using DracoRuan.Foundation.DataFlow.DataMigration.Migrator;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Core.Dependencies
{
    public class MigrationRegistry
    {
        private readonly Dictionary<(string, int), IDataMigrator> _dataMigrators = new();
        private readonly Dictionary<string, SortedSet<int>> _domainVersions = new();

        public void Register(IDataMigrator migrator)
        {
            (string domain, int fromVersion) = (migrator.Domain, migrator.FromVersion);
            this._dataMigrators[(domain, fromVersion)] = migrator;

            if (!this._domainVersions.TryGetValue(domain, out SortedSet<int> versions))
            {
                versions = new SortedSet<int>
                {
                    migrator.FromVersion
                };
                
                this._domainVersions[domain] = versions;
            }
            
            versions.Add(migrator.FromVersion);
        }

        public List<IDataMigrator> GetDataMigratorChain(string domain, int fromVersion, int targetVersion)
        {
            int inspectingVersion = fromVersion;
            List<IDataMigrator> migrationChain = new();
            
            while (inspectingVersion < targetVersion)
            {
                (string, int) key = (domain, inspectingVersion);
                if (!this._dataMigrators.TryGetValue(key, out IDataMigrator migrator))
                {
                    throw new MigrationException(
                        "Do not found valid Data Migrator, please check again the sequence of data migration!");
                }
                
                if (migrator.ToVersion > targetVersion) 
                    continue;
                
                migrationChain.Add(migrator);
                inspectingVersion = migrator.ToVersion;
            }

            if (inspectingVersion != targetVersion)
            {
                throw new MigrationException(
                    $"The Data Migration of {domain} has finished at v{inspectingVersion} but target version is {targetVersion}!");
            }

            return migrationChain;
        }

        public IEnumerable<string> GetAllDomains() => this._domainVersions.Keys;

        public bool HasDataMigratorForDomain(string domain) => this._domainVersions.ContainsKey(domain);
        
        public int GetLatestVersionOfDomain(string domain)
        {
            int version = -1;
            if (!this._dataMigrators.TryGetValue((domain, 0), out _))
            {
                
            }

            if (!this._domainVersions.TryGetValue(domain, out SortedSet<int> versions)) 
                return version;
            
            version = versions.Min;
            while (this._dataMigrators.TryGetValue((domain, version), out IDataMigrator migrator))
                version = migrator.ToVersion;

            return version;
        }
    }
}
