using System;

namespace Mapture
{
    public class MaptureOptions
    {
        public bool CompatibilityMode { get; set; }
        public int MaxDepth { get; set; } = 10;
        public bool EnableCycleDetection { get; set; } = true;
        public bool EnableDebugTracing { get; set; }
    }
}
