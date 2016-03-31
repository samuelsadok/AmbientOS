using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmbientOS.Utils
{
    /// <summary>
    /// Represents a directed graph that is described by a dynamic set of edges.
    /// </summary>
    /// <typeparam name="V">The vertex type</typeparam>
    /// <typeparam name="E">A reference counted edge type</typeparam>
    class DynamicGraph<V, E>
        where E : IRefCounted
    {
        DynamicSet<E> edges;
        Func<E, V> startVertexConverter;
        Func<E, V> endVertexConverter;

        private V GetStartVertex(E edge)
        {
            return startVertexConverter(edge);
        }

        private V GetEndVertex(E edge)
        {
            return endVertexConverter(edge);
        }

        public DynamicGraph(DynamicSet<E> edges, Func<E, V> startVertexConverter, Func<E, V> endVertexConverter)
        {
            this.edges = edges;
            this.startVertexConverter = startVertexConverter;
            this.endVertexConverter = endVertexConverter;
        }

        /// <summary>
        /// Returns a path between the specified start and end vertices.
        /// Returns null if there is no connection between the two vertices.
        /// Caution should be taken, as the path may already be broken by the time it is returned.
        /// todo: make it the least cost path
        /// Implements somthing similar to Dijkstra, I think.
        /// </summary>
        public E[] FindPath(V start, V end)
        {
            var edges = this.edges.Snapshot().ToList();

            var reachableVertices = new List<Tuple<V, IEnumerable<E>>>();
            reachableVertices.Add(new Tuple<V, IEnumerable<E>>(start, new E[0]));

            var currentBorder = reachableVertices;

            while (currentBorder.Any()) {
                var result = currentBorder.FirstOrDefault(v => v.Item1.Equals(end));
                if (result != null)
                    return result.Item2.Select(edge => edge.Retain()).ToArray();

                var newBorder = new List<Tuple<V, IEnumerable<E>>>();

                foreach (var vertex in currentBorder) {
                    var visitedEdges = edges.Where(edge => GetStartVertex(edge).Equals(vertex.Item1)).ToArray();
                    foreach (var edge in visitedEdges) {
                        var newPath = vertex.Item2.Concat(new E[] { edge });
                        var endVertex = GetEndVertex(edge);

                        // to find least cost, we'd have to select each path from either reachableVertices or newBorder
                        if (!reachableVertices.Any(v => v.Item1.Equals(endVertex)))
                            newBorder.Add(new Tuple<V, IEnumerable<E>>(endVertex, newPath));
                    }

                    // to find least cost, we must not throw away edges
                    edges.RemoveAll(e => visitedEdges.Contains(e));
                }

                currentBorder = newBorder.Distinct().ToList();
                reachableVertices.AddRange(currentBorder);
            }

            return null;
        }
    }
}
