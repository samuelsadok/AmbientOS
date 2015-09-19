using CoreGraphics;
using AppInstall.Framework;

namespace AppInstall.Graphics
{

    public abstract partial class PathElement2D : PathElement<float, Vector2D<float>>
    {
        public abstract void Draw(CGPath path);
    }


    public partial class PathLine2D : PathElement2D
    {
        public override void Draw(CGPath path)
        {
            path.AddLineToPoint(Endpoint.X, Endpoint.Y);
        }
    }

    public partial class PathArc2D : PathElement2D
    {
        public override void Draw(CGPath path)
        {
            path.AddArcToPoint(StartTangent.X, StartTangent.Y, EndTangent.X, EndTangent.Y, Radius);
        }
    }

    public partial class PathFreeArc2D : PathElement2D
    {
        public override void Draw(CGPath path)
        {
            path.AddArc(Center.X, Center.Y, Radius, StartAngle, EndAngle, true);
        }
    }
    
    
    /// <summary>
    /// Represents a path in 2D space.
    /// A path can consist of multiple disconnected path segments.
    /// Each path segment consists of multiple path elements.
    /// </summary>
    public partial class Path2D : Path<float, Vector2D<float>, PathElement2D>
    {
        /// <summary>
        /// Returns the platform specific version of this path.
        /// </summary>
        public CGPath NativePath { get { return nativePath; } }
        private CGPath nativePath = new CGPath();


        /// <summary>
        /// Moves the cursor to a new point without placing a line. This starts a new subpath if the point doesn't equal the current point.
        /// </summary>
        public override void MoveToPoint(Vector2D<float> point)
        {
            base.MoveToPoint(point);
            nativePath.MoveToPoint(point.X, point.Y);
        }

        /// <summary>
        /// Adds an element to the current path segment.
        /// The element is placed at the last position and the position is moved to the end of the element.
        /// </summary>
        public override void Add(PathElement2D element)
        {
            base.Add(element);
            element.Draw(nativePath);
        }

        /// <summary>
        /// Closes the current subpath (if any) by connecting the current point with the starting point
        /// </summary>
        public override void CloseSubpath()
        {
            base.CloseSubpath();
            nativePath.CloseSubpath();
        }
    }


}