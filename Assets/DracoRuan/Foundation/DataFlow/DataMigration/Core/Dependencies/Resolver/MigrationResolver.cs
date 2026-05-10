using System.Collections.Generic;
using DracoRuan.Foundation.DataFlow.DataMigration.Core.Exceptions;

namespace DracoRuan.Foundation.DataFlow.DataMigration.Core.Dependencies.Resolver
{
    public class MigrationResolver
    {
        public List<MigrationUnit> BuildTopologicalSortedMigrations(IEnumerable<MigrationUnit> units)
        {
            Dictionary<string, MigrationUnit> unitMap = new();
            foreach (var unit in units)
                unitMap[unit.Domain] = unit;

            List<MigrationUnit> result = new();
            Dictionary<string, ResolveVisitState> visitStateMap = new();
            Stack<string> stackPath = new();

            foreach (var kvp in unitMap)
                visitStateMap[kvp.Key] = ResolveVisitState.NotVisited;

            foreach (string domain in unitMap.Keys)
            {
                if (visitStateMap[domain] == ResolveVisitState.NotVisited)
                    this.Visit(domain, unitMap, visitStateMap, result, stackPath);
            }

            return result;
        }

        private void Visit(string domain,
            Dictionary<string, MigrationUnit> unitMap,
            Dictionary<string, ResolveVisitState> visitStates,
            List<MigrationUnit> sortedUnits,
            Stack<string> stackPath)
        {
            if (!unitMap.TryGetValue(domain, out MigrationUnit unit))
            {
                throw new MigrationException($"Unknown migration unit: {domain}");
            }

            visitStates[domain] = ResolveVisitState.Visiting;
            stackPath.Push(domain);

            foreach (var dependency in unit.Dependencies)
            {
                if (!visitStates.TryGetValue(dependency, out ResolveVisitState visitState))
                {
                    continue;
                }

                if (visitState == ResolveVisitState.Visiting)
                {
                    var cycle = BuildCyclePath(stackPath, dependency);
                    throw new MigrationException(
                        $"Circular dependency detected in migration!\n" +
                        $"Cycle: {cycle}\n" +
                        $"Please check Dependencies of relevanted migrators.");
                }

                if (visitState == ResolveVisitState.NotVisited)
                    this.Visit(dependency, unitMap, visitStates, sortedUnits, stackPath);
            }

            stackPath.Pop();
            visitStates[domain] = ResolveVisitState.Visited;
            sortedUnits.Add(unit);
        }

        private string BuildCyclePath(Stack<string> path, string cycleDomain)
        {
            List<string> pathList = new(path);
            pathList.Reverse();

            int cycleDomainIndex = pathList.IndexOf(cycleDomain);
            if (cycleDomainIndex < 0)
                return $"{cycleDomain} → ...";

            List<string> cycle = pathList.GetRange(cycleDomainIndex, pathList.Count - cycleDomainIndex);
            cycle.Add(cycleDomain);
            return string.Join(" → ", cycle);
        }

        public void ValidateDependenciesExist(IEnumerable<MigrationUnit> units, MigrationRegistry registry)
        {
            List<string> errors = new();
            foreach (var unit in units)
            {
                foreach (string dependency in unit.Dependencies)
                {
                    if (!registry.HasDataMigratorForDomain(dependency))
                    {
                        errors.Add(
                            $"Domain '{unit.Domain}' depends on '{dependency}' but '{dependency}' do not have any registered migrator.");
                    }
                }
            }

            if (errors.Count > 0)
            {
                throw new MigrationException($"Dependency validation failed:\n {string.Join("\n", errors)}");
            }
        }
    }
}
