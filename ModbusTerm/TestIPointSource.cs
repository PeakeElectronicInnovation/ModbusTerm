using NModbus;
using NModbus.Data;
using System;

namespace ModbusTerm
{
    public class TestIPointSource
    {
        public void ShowMethods()
        {
            // Just a placeholder to explore methods available on IPointSource<ushort>
#pragma warning disable CS0219 // Variable is assigned but its value is never used
            IPointSource<ushort>? source = null;
#pragma warning restore CS0219
            
            // This will show compiler errors for unavailable methods
            // and IntelliSense suggestions for available ones
            // source.?
        }
    }
}
