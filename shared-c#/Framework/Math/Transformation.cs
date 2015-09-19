using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppInstall.Framework
{

    public class Quaternion
    {
        public double W { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Z { get; private set; }


        private Quaternion inverse = null;
        private Matrix<double> rotationMatrix = null;

        public Quaternion(double w, double x, double y, double z)
        {
            W = w;
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Creates a unit quaternion representing no rotation.
        /// </summary>
        public Quaternion()
            : this(1, 0, 0, 0)
        {
        }


        /// <summary>
        /// Creates a quaternion that represents the euler angles yaw-pitch-roll (in this order).
        /// All angles must be in radians.
        /// This is equivalent to the MatLab function angle2quat(..., 'XYZ').
        /// </summary>
        /// <param name="xAngle">The rotation about the X-axis (roll)</param>
        /// <param name="yAngle">The rotation about the Y-axis (pitch)</param>
        /// <param name="zAngle">The rotation about the Z-axis (yaw)</param>
        public Quaternion(double xAngle, double yAngle, double zAngle)
        {
            double c1 = Math.Cos(xAngle / 2);
            double s1 = Math.Sin(xAngle / 2);
            double c2 = Math.Cos(yAngle / 2);
            double s2 = Math.Sin(yAngle / 2);
            double c3 = Math.Cos(zAngle / 2);
            double s3 = Math.Sin(zAngle / 2);
            double c1c2 = c1 * c2;
            double s1s2 = s1 * s2;
            W = c1c2 * c3 - s1s2 * s3;
            X = s1 * c2 * c3 + c1 * s2 * s3;
            Y = c1 * s2 * c3 - s1 * c2 * s3;
            Z = c1c2 * s3 + s1s2 * c3;
        }


        /// <summary>
        /// Returns the norm of this quaternion.
        /// norm = W^2 + X^2 + Y^2 + Z^2
        /// This is equivalent to the MatLab function quatnorm.
        /// </summary>
        public double Norm()
        {
            return W * W + X * X + Y * Y + Z * Z;
        }

        /// <summary>
        /// Divides every component of the quaternion by it's norm.
        /// This is equivalent to the MatLab function quatnormalize.
        /// </summary>
        public Quaternion Normalized()
        {
            double q = Math.Sqrt(Norm());
            return new Quaternion(W / q, X / q, Y / q, Z / q);
        }

        /// <summary>
        /// Returns the inverse of this quaternion.
        /// If a quaternion represents a rotation, it's inverse represents the reverse rotation.
        /// This is equivalent to the MatLab function quatinv.
        /// </summary>
        public Quaternion Inverse()
        {
            if (inverse == null) {
                double norm = Norm();
                inverse = new Quaternion(W / norm, -X / norm, -Y / norm, -Z / norm);
            }
            return inverse;
        }

        /// <summary>
        /// Returns a rotation matrix that represents this quaternion.
        /// This is equivalent to the MatLab function quat2dcm.
        /// </summary>
        public Matrix<double> RotationMatrix()
        {
            if (rotationMatrix == null) {
                var q = this.Normalized();

                double[][] values = {
                    new double [] { q.W * q.W + q.X * q.X - q.Y * q.Y - q.Z * q.Z, 2 * (q.X * q.Y + q.W * q.Z), 2 * (q.X * q.Z - q.W * q.Y) },
                    new double [] { 2 * (q.X * q.Y - q.W * q.Z), q.W * q.W - q.X * q.X + q.Y * q.Y - q.Z * q.Z, 2 * (q.Y * q.Z + q.W * q.X) },
                    new double [] { 2 * (q.X * q.Z + q.W * q.Y), 2 * (q.Y * q.Z - q.W * q.X), q.W * q.W - q.X * q.X - q.Y * q.Y + q.Z * q.Z },
                };

                rotationMatrix = new Matrix<double>(values).Transpose();
            }
            return rotationMatrix;
        }

        /// <summary>
        /// Rotates a point or vector by applying this quaternion.
        /// This is equivalent to the MatLab function quatrotate.
        /// </summary>
        public Vector3D<double> Rotate(Vector3D<double> point)
        {
            return (Vector3D<double>)(RotationMatrix() * point);
        }

        /// <summary>
        /// Returns the quaternion product a * b.
        /// The product is equivalent to first applying b and then a.
        /// </summary>
        public static Quaternion operator *(Quaternion a, Quaternion b)
        {
            return new Quaternion(
                b.W * a.W - b.X * a.X - b.Y * a.Y - b.Z * a.Z,
                b.W * a.X + b.X * a.W - b.Y * a.Z + b.Z * a.Y,
                b.W * a.Y + b.X * a.Z + b.Y * a.W - b.Z * a.X,
                b.W * a.Z - b.X * a.Y + b.Y * a.X + b.Z * a.W
                );
        }

        /// <summary>
        /// Returns a string that contains the value of the quaternion
        /// </summary>
        public override string ToString()
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return "{ " + W.ToString(c) + " + " + X.ToString(c) + "i + " + Y.ToString(c) + "j + " + Z.ToString(c) + "k }";
        }
    }



    public class YawPitchRoll
    {
        public float Yaw { get; set; }
        public float Pitch { get; set; }
        public float Roll { get; set; }

        public YawPitchRoll(float yaw, float pitch, float roll)
        {
            Yaw = yaw;
            Pitch = pitch;
            Roll = roll;
        }
        public YawPitchRoll()
        {

        }

        public static float RadianToDegrees(float radian)
        {
            return radian / (float)Math.PI * 180f;
        }

        /// <summary>
        /// Returns a string that contains the YPR values
        /// </summary>
        public override string ToString()
        {
            return "{ y = " + RadianToDegrees(Yaw) + "°, p = " + RadianToDegrees(Pitch) + "°, r = " + RadianToDegrees(Roll) + "° }";
        }
    }
}