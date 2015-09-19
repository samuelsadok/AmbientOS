using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppInstall.Framework
{

    public static class LinearRegression
    {
        /// <summary>
        /// Solves a linear regression problem through a set of points for the provided linear function
        /// </summary>
        /// <param name="function">The function that, for a given point, returns a set of coefficients (a) and a right side (y) to describe a linear equation of the type "c1*x1 + c2*x2 + x3*c3 = y"</param>
        /// <returns>A vector of the dimension specified in the variables parameter that contains the solution</returns>
        public static Vector<T> Solve<T, TP>(IEnumerable<TP> points, int variables, Func<TP, Tuple<Vector<T>, T>> function)
        {
            Matrix<T> leftSide = new Matrix<T>(points.Count(), variables);
            Vector<T> rightSide = new Vector<T>(points.Count());

            int i = 0;
            foreach (var point in points) {
                var equation = function(point);
                for (int j = 0; j < variables; j++)
                    leftSide[i, j] = equation.Item1[j];
                rightSide[i++] = equation.Item2;
            }

            return leftSide.LinearRegression(rightSide);
        }


        /// <summary>
        /// Finds the circle that trys to align with the specified points.
        /// </summary>
        /// <remarks>The as of mid 2014, due limitations of the Mono AOT, this function must not be generic (the lambda expression raises problems)</remarks>
        public static Tuple<Vector2D<float>, float> BestCircle(IEnumerable<Vector2D<float>> points)
        {
            // the circle "(x - mx)^2 + (y - my)^2 = r^2" is reduced to the linear equation "2*x*mx + 2*y*my + c = x^2 + y^2" with c = r^2 - mx^2 - my^2
            Func<Vector2D<float>, Tuple<Vector<float>, float>> function = (p) => new Tuple<Vector<float>, float>(new Vector<float>(Scalar.Add(p.X, p.X), Scalar.Add(p.Y, p.Y), Scalar.MultiplicativeNeutralElement<float>()), Scalar.Add(Scalar.Square(p.X), Scalar.Square(p.Y)));
            var result = Solve(points, 3, function);
            return new Tuple<Vector2D<float>, float>(new Vector2D<float>(result[0], result[1]), Scalar.SquareRoot(Scalar.Add(result[2], Scalar.Add(Scalar.Square(result[0]), Scalar.Square(result[1])))));
        }
    }
}