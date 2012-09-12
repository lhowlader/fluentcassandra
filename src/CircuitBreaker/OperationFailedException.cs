using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FluentCassandra.CircuitBreaker
{
    /// <summary>
    /// Exception thrown when an attempted operation has failed.
    /// </summary>
    public class OperationFailedException : ApplicationException
    {
        public OperationFailedException() 
            : base()
        {
        }

        public OperationFailedException(string message) 
            : base(message)
        {
        }

        public OperationFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
