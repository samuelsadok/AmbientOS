using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppInstall.Framework;

namespace AppInstall.Graphics
{

    /// <summary>
    /// Represents a path in an arbitrary vector space.
    /// A path can consist of multiple disconnected path segments.
    /// Each path segment consists of multiple path elements.
    /// </summary>
    public class Path<TScalar, TVector, TElement>
        where TVector : Vector<TScalar>, new()
        where TElement : PathElement<TScalar, TVector>
    {

        private class PathSegment
        {
            public TVector Start;
            public TVector CurrentPoint;
            public LinkedList<TElement> Elements = new LinkedList<TElement>();
            public bool Closed = false;
        }

        private LinkedList<PathSegment> segments = new LinkedList<PathSegment>();

        bool changed = true;

        /// <summary>
        /// Moves the cursor to a new point without placing a line. This starts a new subpath if the point doesn't equal the current point.
        /// </summary>
        public virtual void MoveToPoint(TVector point)
        {
            if (segments.Any())
                if (!segments.Last().Elements.Any())
                    segments.RemoveLast();

            if (segments.Any())
                if (segments.Last().CurrentPoint == point)
                    return;

            segments.AddLast(new PathSegment() { Start = point });
            changed = true;
        }

        /// <summary>
        /// Adds an element to the current path segment.
        /// The element is placed at the last position and the position is moved to the end of the element.
        /// </summary>
        public virtual void Add(TElement element)
        {
            if (!segments.Any())
                MoveToPoint(new TVector());

            segments.Last().Elements.AddLast(element);
            // todo: update position
            changed = true;
        }

        /// <summary>
        /// Closes the current subpath (if any) by connecting the current point with the starting point
        /// </summary>
        public virtual void CloseSubpath()
        {
            if (!segments.Any())
                return;
            segments.Last().Closed = true;
        }
    }


    /// <summary>
    /// Represents a path in 2D space.
    /// A path can consist of multiple disconnected path segments.
    /// Each path segment consists of multiple path elements.
    /// This class can be augmented by platform specific functionality.
    /// </summary>
    public partial class Path2D : Path<float, Vector2D<float>, PathElement2D>
    {
        public void MoveToPoint(float x, float y)
        {
            MoveToPoint(new Vector2D<float>(x, y));
        }

        /// <summary>
        /// Moves the cursor to the specified point and places a line
        /// </summary>
        public void AddLine(float x, float y)
        {
            AddLine(new Vector2D<float>(x, y));
        }
        public void AddLine(Vector2D<float> point)
        {
            Add(new PathLine2D(point));
        }

        public void AddArc(Vector2D<float> startTangent, Vector2D<float> endTangent, float radius)
        {
            Add(new PathArc2D(startTangent, endTangent, radius));
        }

        public void AddArc(Vector2D<float> center, float radius, float startAngle, float endAngle)
        {
            Add(new PathFreeArc2D(center, radius, startAngle, endAngle));
        }
    }


    /// <summary>
    /// Represents a path element in some arbitrary vector space.
    /// </summary>
    public abstract class PathElement<TScalar, TVector>
        where TVector : Vector<TScalar>
    {
    }

    /// <summary>
    /// Represents a path element in 2D space.
    /// </summary>
    public abstract partial class PathElement2D : PathElement<float, Vector2D<float>>
    {
    }

    /// <summary>
    /// Represents a straight line to some end point.
    /// </summary>
    public partial class PathLine2D : PathElement2D
    {
        public Vector2D<float> Endpoint { get; private set; }
        public PathLine2D(Vector2D<float> endpoint)
        {
            Endpoint = endpoint;
        }
    }

    /// <summary>
    /// Represents an arc that is defined by the current path point, two tangents and the radius.
    /// </summary>
    public partial class PathArc2D : PathElement2D
    {
        public Vector2D<float> StartTangent { get; private set; }
        public Vector2D<float> EndTangent { get; private set; }
        public float Radius { get; private set; }
        public PathArc2D(Vector2D<float> startTangent, Vector2D<float> endTarget, float radius)
        {
            StartTangent = startTangent;
            EndTangent = endTarget;
            Radius = radius;
        }
    }

    /// <summary>
    /// Represents a free standing arc that is defined by it's center point, radius and start and end angles.
    /// </summary>
    public partial class PathFreeArc2D : PathElement2D
    {
        public Vector2D<float> Center { get; private set; }
        public float Radius { get; private set; }
        public float StartAngle { get; private set; }
        public float EndAngle { get; private set; }
        public PathFreeArc2D(Vector2D<float> center, float radius, float startAngle, float endAngle)
        {
            Center = center;
            Radius = radius;
            StartAngle = startAngle;
            EndAngle = endAngle;
        }
    }

}