using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppInstall.Framework
{

    public interface IPhysicalQuantity
    {
        /// <summary>
        /// Returns various display names (such as "m", "km", ...)
        /// </summary>
        string[] GetUnitNames();
        /// <summary>
        /// Returns a human readably form of this quantity
        /// </summary>
        string ToString();
        /// <summary>
        /// Specifies the preferred unit index
        /// </summary>
        int PreferredUnit { get; set; }
        /// <summary>
        /// Returns true if this quantity can only have integer values
        /// </summary>
        bool IsInteger { get; }
        /// <summary>
        /// Returns the quantity in the specified unit
        /// </summary>
        float GetQuantity(int unitIndex);
        /// <summary>
        /// Sets this quantity.
        /// </summary>
        /// <param name="unitNameIndex">A reference to the used unit in the array returned by GetUnitNames()</param>
        void SetQuantity(float quantity, int unitNameIndex);
    }

    public abstract class PhysicalQuantity : IPhysicalQuantity
    {
        private float quantity = 1f;
        private string[] unitNames;
        private string[] unitNamesShort;
        private float[] unitMultipliers;

        public PhysicalQuantity(string[] unitNames, string[] unitNamesShort, float[] unitMultipliers, bool isInteger)
        {
            this.unitNames = unitNames;
            this.unitNamesShort = unitNamesShort;
            this.unitMultipliers = unitMultipliers;
            IsInteger = isInteger;
        }

        public int PreferredUnit { get; set; }
        public bool IsInteger { get; private set; }

        public string[] GetUnitNames()
        {
            return unitNames;
        }

        public override string ToString()
        {
            return (quantity / unitMultipliers[0]) + unitNamesShort[0]; // todo: select appropriate unit
        }
        
        public float GetQuantity(int unitIndex)
        {
            return quantity / unitMultipliers[unitIndex];
        }
        public void SetQuantity(float quantity, int unitIndex)
        {
            this.quantity = quantity * unitMultipliers[unitIndex];
        }
    }






    public class NaturalQuantity : PhysicalQuantity
    {
        public NaturalQuantity()
            : base(new string[] { "pieces" },
                   new string[] { "pcs" },
                   new float[] { 1 }, true)
        {
        }
    }

    public class VolumeQuantity : PhysicalQuantity
    {
        public VolumeQuantity()
            : base(new string[] { "liters" },
                   new string[] { "L" },
                   new float[] { 1 }, true)
        {
        }
    }

    public class LengthQuantity : PhysicalQuantity
    {
        public LengthQuantity()
            : base(new string[] { "meters" },
                   new string[] { "m" },
                   new float[] { 1 }, false)
        {
        }
    }

    public class WeightQuantity : PhysicalQuantity
    {
        public WeightQuantity()
            : base(new string[] { "grams", "kilograms", "metric tons" },
                   new string[] { "g", "kg", "t" },
                   new float[] { 0.001f, 1f, 1000f }, false)
        {
        }
    }
}