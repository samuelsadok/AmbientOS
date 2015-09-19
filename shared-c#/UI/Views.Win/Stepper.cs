using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppInstall.UI
{
    public class Stepper
    {
        public event Action<double> ValueChanged;

        public double Value { get; set; }
        public double StepSize { get; set; }

        public Stepper()
        {
            throw new NotImplementedException();
        }
    }
}
