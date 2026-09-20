using System;
using System.Collections.Generic;

namespace DracoRuan.Foundation.DataFlow.Core.Migration
{
    /// <summary>
    /// Tarjan's strongly connected components, plus a topological order over the condensation.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this and not a plain topological sort.</b> A topological sort can only order a
    /// graph with no cycles, so the moment two domains depend on each other it has to give up — the
    /// previous resolver threw on any cycle, which meant mutually dependent save data was simply
    /// unsupported. Condensing each cycle into a single node turns <i>any</i> dependency graph into
    /// a DAG, so an order always exists. The cycles do not disappear; they become units that must be
    /// migrated together, which is the honest answer.</para>
    ///
    /// <para>Iterative rather than recursive: a deep dependency chain would otherwise risk a stack
    /// overflow on the boot path, where there is nothing to catch it.</para>
    /// </remarks>
    public static class StronglyConnectedComponents
    {
        /// <summary>
        /// Finds the strongly connected components of a directed graph.
        /// </summary>
        /// <param name="nodes">Every node.</param>
        /// <param name="getEdges">Outgoing edges — "this node depends on those nodes".</param>
        /// <returns>
        /// Components in reverse topological order: a component appears before every component that
        /// depends on it, which is exactly execution order for dependencies.
        /// </returns>
        public static List<List<string>> Find(
            IReadOnlyCollection<string> nodes,
            Func<string, IEnumerable<string>> getEdges)
        {
            Dictionary<string, int> index = new(StringComparer.Ordinal);
            Dictionary<string, int> lowLink = new(StringComparer.Ordinal);
            HashSet<string> onStack = new(StringComparer.Ordinal);
            Stack<string> stack = new();
            List<List<string>> components = new();
            int nextIndex = 0;

            foreach (string start in nodes)
            {
                if (index.ContainsKey(start))
                    continue;

                // Explicit work stack: each frame is a node plus its not-yet-visited neighbours.
                Stack<(string Node, IEnumerator<string> Edges)> work = new();

                index[start] = nextIndex;
                lowLink[start] = nextIndex;
                nextIndex++;
                stack.Push(start);
                onStack.Add(start);
                work.Push((start, getEdges(start).GetEnumerator()));

                while (work.Count > 0)
                {
                    (string node, IEnumerator<string> edges) = work.Peek();

                    if (edges.MoveNext())
                    {
                        string next = edges.Current;
                        if (next == null)
                            continue;

                        if (!index.ContainsKey(next))
                        {
                            index[next] = nextIndex;
                            lowLink[next] = nextIndex;
                            nextIndex++;
                            stack.Push(next);
                            onStack.Add(next);
                            work.Push((next, getEdges(next).GetEnumerator()));
                        }
                        else if (onStack.Contains(next))
                        {
                            lowLink[node] = Math.Min(lowLink[node], index[next]);
                        }

                        continue;
                    }

                    work.Pop();
                    edges.Dispose();

                    // Propagate the low-link up to the parent frame.
                    if (work.Count > 0)
                    {
                        string parent = work.Peek().Node;
                        lowLink[parent] = Math.Min(lowLink[parent], lowLink[node]);
                    }

                    // Root of a component: pop everything above it off the stack.
                    if (lowLink[node] != index[node])
                        continue;

                    List<string> component = new();
                    string member;
                    do
                    {
                        member = stack.Pop();
                        onStack.Remove(member);
                        component.Add(member);
                    } while (!string.Equals(member, node, StringComparison.Ordinal));

                    component.Sort(StringComparer.Ordinal);
                    components.Add(component);
                }
            }

            return components;
        }
    }
}
