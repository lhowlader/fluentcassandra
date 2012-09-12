using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FluentCassandra.CircuitBreaker
{
    /// <summary>
    /// Exception thrown when an operation is being called on an open circuit.
    /// </summary>
    public class OpenCircuitException : ApplicationException
    {
        public OpenCircuitException() 
            : base()
        {
        }

        public OpenCircuitException(string message) 
            : base(message)
        {
        }

        public OpenCircuitException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}


