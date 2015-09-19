using System;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{

    //public class Direction
    //{
    //    private int direction;
    //
    //
    //    public bool IsUpwards { get { return direction == 0; } }
    //    public bool IsToTheRight { get { return direction == 1; } }
    //    public bool IsDownwards { get { return direction == 2; } }
    //    public bool IsToTheLeft { get { return direction == 3; } }
    //
    //    private Direction(int direction)
    //    {
    //        this.direction = direction % 4;
    //    }
    //
    //    public static Direction Upwards()
    //    {
    //        return new Direction(0);
    //    }
    //
    //    public static Direction ToTheRight()
    //    {
    //        return new Direction(1);
    //    }
    //
    //    public static Direction Downwards()
    //    {
    //        return new Direction(2);
    //    }
    //
    //    public static Direction ToTheLeft()
    //    {
    //        return new Direction(3);
    //    }
    //
    //    public void RotateClockwise(int steps)
    //    {
    //        direction = (direction + steps) % 4;
    //        if (direction < 0) direction += 4;
    //    }
    //}

    public enum TextAlignment
    {
        Left,
        Center,
        Right,
        Justified
    }

    //public partial class Alignment
    //{
    //    public bool Left { get; private set; }
    //    public bool Right { get; private set; }
    //    public bool Top { get; private set; }
    //    public bool Bottom { get; private set; }
    //
    //    public Alignment(bool left, bool right, bool top, bool bottom)
    //    {
    //        Left = left;
    //        Right = right;
    //        Top = top;
    //        Bottom = bottom;
    //    }
    //}




    public partial class Animation
    {
        private Action action;
        private Action endAction;

        /// <summary>
        /// Creates a new animation with the specified completition handler.
        /// </summary>
        /// <param name="action">The action to be animated. Can be null.</param>
        /// <param name="endAction">The completition handler. Can be null.</param>
        public Animation(Action action, Action endAction)
        {
            this.action = action;
            this.endAction = endAction;
        }

        /// <summary>
        /// Executes the animation.
        /// </summary>
        /// <param name="duration">The duration in milliseconds. Can be zero.</param>
        public void Execute(int duration)
        {
            if (duration == 0) {
                InvokeAnimatedAction();
                InvokeEndAction();
            } else {
                PlatformExecute(duration);
            }
        }

        private void InvokeAnimatedAction()
        {
            action.SafeInvoke();
        }
        private void InvokeEndAction()
        {
            endAction.SafeInvoke();
        }
    }


    /// <summary>
    /// Represents a common interface for different types of text boxes
    /// </summary>
    public interface ITextBox
    {
        event Action<ITextBox> TextChanged;
        Color TextColor { get; set; }
        string Text { get; set; }
        bool IsReadOnly { get; set; }
    }

}