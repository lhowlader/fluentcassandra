using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace FluentCassandra.CircuitBreaker
{
    public enum CircuitBreakerState
    {
        Closed,
        Open,
        HalfOpen
    }

    /// <summary>
    /// Singleton Implementation of the Circuit Breaker pattern.
    /// </summary>
    public sealed class CircuitBreaker
    {
        private static volatile CircuitBreaker instance;
        private static object syncRoot = new Object();

        private CircuitBreaker() { }

        public static CircuitBreaker Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                            instance = new CircuitBreaker(5, 60000);
                    }
                }
                return instance;
            }
        }

        public event EventHandler StateChanged;
        public event EventHandler ServiceLevelChanged;

        private uint threshold;
        private int failureCount;
        private readonly System.Timers.Timer timer;
        private CircuitBreakerState state;
        private IList<Type> ignoredExceptionTypes;

        /// <summary>
        /// Number of failures allowed before the circuit trips.
        /// </summary>
        public uint Threshold
        {
            get { return this.threshold; }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Threshold must be greater than zero");
                }

                this.threshold = value;
            }
        }

        /// <summary>
        /// The time, in milliseconds, before the circuit attempts to close after being tripped.
        /// </summary>
        public uint Timeout
        {
            get { return (uint)this.timer.Interval; }
            set { this.timer.Interval = value; }
        }

        /// <summary>
        /// List of operation exception types the circuit breaker ignores.
        /// </summary>
        public IList<Type> IgnoredExceptionTypes
        {
            get { return this.ignoredExceptionTypes; }
        }

        /// <summary>
        /// The current service level of the circuit.
        /// </summary>
        public double ServiceLevel
        {
            get { return ((this.threshold - (double)this.failureCount) / this.threshold) * 100; }
        }

        /// <summary>
        /// Current state of the circuit breaker.
        /// </summary>
        public CircuitBreakerState State
        {
            get { return this.state; }
        }

        
        public CircuitBreaker(uint threshold, uint timeout)
        {
            this.threshold = threshold;
            this.failureCount = 0;
            this.state = CircuitBreakerState.Closed;
            this.ignoredExceptionTypes = new List<Type>();

            this.timer = new System.Timers.Timer(timeout);
            this.timer.Elapsed += new System.Timers.ElapsedEventHandler(timer_Elapsed);
        }

        /// <summary>
        /// Executes the operation.
        /// </summary>
        /// <param name="operation">Operation to execute</param>
        /// <param name="args">Operation arguments</param>
        /// <returns>Result of operation as an object</returns>
        /// <exception cref="OpenCircuitException"></exception>
        public object Execute(Delegate operation, params object[] args)
        {
            if (this.state == CircuitBreakerState.Open)
            {
                throw new OpenCircuitException("Circuit breaker is currently open");
            }

            object result = null;
            try
            {
                // Execute operation
                result = operation.DynamicInvoke(args);
            }
            catch (Exception ex)
            {
                if (ex.InnerException == null)
                {
                    // If there is no inner exception, then the exception was caused by the invoker, so throw
                    throw;
                }

                if (this.ignoredExceptionTypes.Contains(ex.InnerException.GetType()))
                {
                    // If exception is one of the ignored types, then throw original exception
                    throw ex.InnerException;
                }

                if (this.state == CircuitBreakerState.HalfOpen)
                {
                    // Operation failed in a half-open state, so reopen circuit
                    Trip();
                }
                else if (this.failureCount < this.threshold)
                {
                    // Operation failed in an open state, so increment failure count and throw exception
                    Interlocked.Increment(ref this.failureCount);

                    OnServiceLevelChanged(new EventArgs());
                }
                else if (this.failureCount >= this.threshold)
                {
                    // Failure count has reached threshold, so trip circuit breaker
                    Trip();
                }

                throw new OperationFailedException("Operation failed", ex.InnerException);
            }

            if (this.state == CircuitBreakerState.HalfOpen)
            {
                // If operation succeeded without error and circuit breaker 
                // is in a half-open state, then reset
                Reset();
            }

            if (this.failureCount > 0)
            {
                // Decrement failure count to improve service level
                Interlocked.Decrement(ref this.failureCount);

                OnServiceLevelChanged(new EventArgs());
            }

            return result;
        }

        /// <summary>
        /// Trips the circuit breaker if not already open.
        /// </summary>
        public void Trip()
        {
            if (this.state != CircuitBreakerState.Open)
            {
                ChangeState(CircuitBreakerState.Open);

                this.timer.Start();
            }
        }

        /// <summary>
        /// Resets the circuit breaker.
        /// </summary>
        public void Reset()
        {
            if (this.state != CircuitBreakerState.Closed)
            {
                ChangeState(CircuitBreakerState.Closed);
                this.failureCount = 0;
                this.timer.Stop();
            }
        }

        /// <summary>
        /// Handles state change logic.
        /// </summary>
        /// <param name="newState"></param>
        private void ChangeState(CircuitBreakerState newState)
        {
            // Change the circuit breaker state
            this.state = newState;

            // Raise changed event
            this.OnCircuitBreakerStateChanged(new EventArgs());
        }

        private void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (this.State == CircuitBreakerState.Open)
            {
                // Attempt to close circuit by switching to a half-open state
                ChangeState(CircuitBreakerState.HalfOpen);

                this.timer.Stop();
            }
        }

        private void OnCircuitBreakerStateChanged(EventArgs e)
        {
            if (this.StateChanged != null)
            {
                StateChanged(this, e);
            }
        }

        private void OnServiceLevelChanged(EventArgs e)
        {
            if (this.ServiceLevelChanged != null)
            {
                ServiceLevelChanged(this, e);
            }
        }

    }
}
