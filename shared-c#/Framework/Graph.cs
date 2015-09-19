using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppInstall.Framework
{
    public class Graph<V, E>
    {
        List<V> vertices;
        List<Tuple<int, int, E>> edges; // from - to - attribute

        public void AddVertex(V vertex)
        {
            vertices.Add(vertex);
        }

        /// <summary>
        /// Finds the minimum spanning tree using Kruskals algorithm with specified weight comparision function.
        /// </summary>
        public List<Tuple<int, int, E>> MinSpanTree(Comparison<E> weightComparision)
        {
            edges.Sort((x, y) => weightComparision(x.Item3, y.Item3));
            UnionFind<V> vertexGroups = new UnionFind<V>(vertices.ToArray());
            return edges.Where((edge) => vertexGroups.Union(edge.Item1, edge.Item2)).ToList(); // starting from the cheapest edge, always select the next edge that does not create a cycle
        }

    }

    //public class DirectedGraph<V, E>
    //{
    //    List<Tuple<V, List<int>, List<int>>> vertices;  // key - {outgoing edge IDs} - {incoming edge IDs}
    //    List<Tuple<int, int, E>> edges; // source - target - attribute
    //
    //    /// <summary>
    //    /// Tries to find a path that conencts two nodes.
    //    /// </summary>
    //    /// <param name="outgoingConnections">Should return a enumeration of tuples (edge to be used, capacity) of all vetices that can be reached directly from a specified vertex</param>
    //    /// <returns>The capacity and the edge indices of the path that was found. Null if there is no path.</returns>
    //    public E FindAndAdjustFlowPath(int fromVertex, int toVertex,  E[] currentFlow, E maxCapacity, List<int> visitedVertices, Func<E, E, E> isLarger, Func<E, E, E> subtracttt, Func<E, bool> isPositive, E maxValue)
    //    {
    //        if (fromVertex == toVertex) return maxValue;
    //
    //        foreach (var forwardEdge in vertices[fromVertex].Item2) {
    //            FindAndAdjustFlowPath(edges[forwardEdge].Item2 , toVertex, currentFlow, visitedVertices, )
    //        }
    //    }
    //
    //    public void MaxFlow(Func<E, E, E> subtracttt, Func<E, bool> isPositive)
    //    {
    //        E[] currentFlow = new E[edges.Count()];
    //        //var v = vertices[0];
    //
    //        var remainderGraph = new Tuple<int, IEnumerable<Tuple<int, E>>>[vertices.Count()];
    //        for (int v = 0; v < vertices.Count(); v++)
    //            remainderGraph[v] = new Tuple<int,IEnumerable<Tuple<int,E>>>(v,
    //                                (from i in vertices[v].Item2 select new Tuple<int, E>(edges[i].Item2, subtracttt(edges[i].Item3, currentFlow[i]))) // forward capacities
    //                        .Concat((from i in vertices[v].Item3 select new Tuple<int, E>(edges[i].Item1, currentFlow[i]))) // reverse capacities
    //                        .Where((e) => isPositive(e.Item2)));
    //    }
    //
    //}
}