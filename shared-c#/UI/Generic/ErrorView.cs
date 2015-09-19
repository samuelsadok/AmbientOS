using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppInstall.OS;
using AppInstall.Framework;
using AppInstall.Graphics;

namespace AppInstall.UI
{
    class ErrorView : GridLayout
    {
        private Label msgLabel = new Label() {
            TextAlignment = TextAlignment.Center,
            FontSize = 20f
        };

        public string Message { get { return msgLabel.Text; } set { msgLabel.Text = value; } }

        public ErrorView()
            : base(5, 1)
        {
            TriangleSign sign = new TriangleSign(TriangleSign.SignType.ExclamationMark, Platform.ScreenSize.GetSmallest() / 3);

            GridLayout innerGrid = new GridLayout(1, 3);
            //innerGrid.RelativeRowHeights[0] = 1;
            //innerGrid.AbsoluteColumnWidths[0] = Platform.ScreenSize.GetSmallest() / 3;
            //innerGrid.AbsoluteColumnWidths[2] = Platform.ScreenSize.GetSmallest() / 3;
            innerGrid.RelativeColumnWidths[0] = 1;
            innerGrid.RelativeColumnWidths[1] = 0;
            innerGrid.RelativeColumnWidths[2] = 1;
            innerGrid[0, 1] = sign;

            RelativeColumnWidths[0] = 1;
            RelativeRowHeights[0] = 1f;
            RelativeRowHeights[1] = 0f;
            RelativeRowHeights[2] = 1f;
            RelativeRowHeights[3] = 0f;
            RelativeRowHeights[4] = 1f;

            this[1, 0] = innerGrid;
            this[3, 0] = msgLabel;

            BackgroundColor = Color.Black;
        }
    }

    class TriangleSign : Canvas
    {
        private static float TRIANGLE_HEIGHT = 0.86602540378f;
        private static float TRIANGLE_BORDER = 1 / 20.0f;
        private static float EXC_MARK_HEIGHT = 0.5f * TRIANGLE_HEIGHT;
        private static float EXC_MARK_WIDTH = 0.3f * EXC_MARK_HEIGHT;
        private static float EXC_POINT_OFFSET = 0.35f * TRIANGLE_HEIGHT;
        private static float EXC_POINT_R = EXC_MARK_WIDTH;


        public enum SignType
        {
            Empty,
            ExclamationMark
        }

        public TriangleSign(SignType type, float size)
        {
            PreserveAspectRatio = true;

            AddPath(createTriangle(size, size * TRIANGLE_HEIGHT), Color.Clear, Color.Yellow, size * TRIANGLE_BORDER);

            switch (type) {
                case SignType.Empty: break;
                case SignType.ExclamationMark :
                    AddPath(createExclamationMark(size * EXC_MARK_WIDTH, size * EXC_MARK_HEIGHT, size * EXC_POINT_OFFSET), Color.Yellow, Color.Clear, 0);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }


        private Path2D createTriangle(float width, float height)
        {
            Path2D p = new Path2D();
            p.MoveToPoint(0, -height / 2);
            p.AddLine(width / 2, height / 2);
            p.AddLine(-width / 2, height / 2);
            p.CloseSubpath();
            return p;
        }

        private Path2D createExclamationMark(float width, float height, float pointOffset)
        {
            Path2D p = new Path2D();

            p.AddArc(new Vector2D<float>(0, -height / 2 + width / 2), width / 2, 0, (float)Math.PI);
            p.AddArc(new Vector2D<float>(0, height * 1.7f), new Vector2D<float>(width / 2, -height / 2 + width / 2), width / 3);
            p.CloseSubpath();
        
            p.AddArc(new Vector2D<float>(0, pointOffset), width / 2, 0, 2 * (float)Math.PI);
            p.CloseSubpath();
        
            return p;
        }
    }
}