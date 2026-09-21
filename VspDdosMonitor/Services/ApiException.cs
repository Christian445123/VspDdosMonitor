using System;

namespace VspDdosMonitor.Services
{
    public sealed class ApiException : Exception
    {
        public int StatusCode { get; }

        public ApiException(string message, int statusCode = 0) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
