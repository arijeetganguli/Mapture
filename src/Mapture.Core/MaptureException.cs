using System;

namespace Mapture
{
    public class MaptureException : Exception
    {
        public MaptureException(string message) : base(message) { }
        public MaptureException(string message, Exception innerException) : base(message, innerException) { }
    }
}
