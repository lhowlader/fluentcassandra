using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using System.Threading;


namespace FluentCassandra.CircuitBreaker
{
    public class CircuitBreakerTests
    {
        
        // Fixture
        CircuitBreaker cb;
        

        private delegate int TestDelegate(int a, int b);

        public int ValidOperation(int a, int b)
        {
            return a + b;
        }

        public void FailedOperation()
        {
            throw new TimeoutException("Network not available");
        }

        //setup
        public void SetUp()
        {
            cb = CircuitBreaker.Instance;
            cb.Reset();
            cb.Threshold = 5;
            cb.Timeout = 60000;
            
        }


        public void TearDown()
        {
        }

        // End of Fixture
        
        
        [Fact]
        public void CircuitBreakerTest()
        {
            // Since the circuit breaker is a singleton, to produce known values for
            // tests require them to be run sequenctially. Xunit tests/Facts does not
            // run sequentially and can alter test results.
            CanCreateCircuitBreaker(); //Tests whether CircuitBreaker can be created
            bool result;
            //*********** SetCircuitBreakerProperties
            cb.Threshold = 10;
            cb.Timeout = 120000;

            //Should update threshold value
            Assert.True(cb.Threshold == 10);
            //Should update timeout value
            Assert.True(cb.Timeout == 120000);
            //*********** End of SetCircuitBreakerProperties
            // ******* CannotSetInvalidThreshold
            result = false;
            try
            {
                cb.Threshold = 0;
            }
            catch (ArgumentException e)
            {
                result = true;
            }
            Assert.True(result);
            // ******* end of CannotSetInvalidThreshold
            // ********* CanExecuteOperation
            object result1 = cb.Execute(new TestDelegate(ValidOperation), 1, 2);

            Assert.NotNull(result1);
            //Should return result of operation
            Assert.True((int)result1 == 3);
            // ********* End of CanExecuteOperation
            // ********* CanGetFaliureCount
            SetUp();
            try
            {
                cb.Execute(new ThreadStart(FailedOperation));
            }
            catch (OperationFailedException) { }

            Assert.True(cb.ServiceLevel == 80, "Service level should have changed and current level is " + cb.ServiceLevel.ToString());

            try
            {
                cb.Execute(new ThreadStart(FailedOperation));
            }
            catch (OperationFailedException) { }
            Assert.True(cb.ServiceLevel == 60, "Service level should have changed again");

            cb.Execute(new TestDelegate(ValidOperation), 1, 2);

            Assert.True(cb.ServiceLevel == 80, "Operation should have succeeded and the service level improved");
            // ********* end of CanGetFailureCount
            
            // ********* CanGetOriginalException()
            Exception innerException = null;
            try
            {
                cb.Execute(new ThreadStart(FailedOperation));
            }
            catch (OperationFailedException ex)
            {
                innerException = ex.InnerException;
            }
            //Should contain original exception
            Assert.IsType(typeof(TimeoutException), innerException);
            // ********* end of CanGetOriginalException()
            // ******** CanTripBreaker
            SetUp();
            result = false;
            for (int i = 0; i < cb.Threshold + 5; i++)
            {
                try
                {
                    cb.Execute(new ThreadStart(FailedOperation));
                }
                catch (OperationFailedException) { }
                catch (OpenCircuitException)
                {
                    result = true;
                    Assert.True(cb.ServiceLevel == 0, "Service level should be zero");

                }
            }
            Assert.True(result);
            // ******** End of CanTripBreaker
            // ***** CanResetBreaker
            result = false;
            try
            {
                cb.Execute(new ThreadStart(FailedOperation));
            }
            catch (OperationFailedException) { }
            catch (OpenCircuitException)
            {
                result = true;
                cb.Reset();
                //Circuit should be closed on reset
                Assert.True(CircuitBreakerState.Closed == cb.State);
                //Service level should remain zero
                Assert.True(0 == cb.ServiceLevel, "Service level is " + cb.ServiceLevel.ToString());
            }
            Assert.True(result);
            /*CanForceTripBreaker();
            // ***** End of CanResetBreaker
            // **** CanForceTripBreaker
            result = false;
            Assert.True(CircuitBreakerState.Closed == cb.State, "Circuit should be initially closed");

            cb.Trip();

            Assert.True(CircuitBreakerState.Open == cb.State, "Circuit should be open on trip");

            // Calling execute when circuit is tripped should throw an OpenCircuitException
            try
            {
                cb.Execute(new TestDelegate(ValidOperation), 1, 2);
            }
            catch (OpenCircuitException e)
            {
                result = true;
            }
            Assert.True(result);
            // **** End of CanForceTripBreaker
            // **** CanForceResetBreaker
            Assert.True(CircuitBreakerState.Closed == cb.State, "Circuit should be initially closed");

            cb.Trip();

            Assert.True(CircuitBreakerState.Open == cb.State, "Circuit should be open on trip");

            cb.Reset();

            Assert.True(CircuitBreakerState.Closed == cb.State, "Circuit should closed on reset");
            Assert.True(100 == cb.ServiceLevel, "Service level should still be 100 percent");

            result1 = cb.Execute(new TestDelegate(ValidOperation), 1, 2);

            Assert.NotNull(result1);
            Assert.True(3 == (int)result1, "Should return result of operation");
            // **** End of CanForceResetBreaker
            // **** CanCloseBreakerAfterTimeout
            cb.Timeout = 500; // Shorten timeout to 500 milliseconds

            cb.Trip();

            Assert.True(CircuitBreakerState.Open == cb.State, "Circuit should be open on trip");

            Thread.Sleep(1000);

            Assert.True(CircuitBreakerState.HalfOpen == cb.State, "Circuit should be half-open after timeout");

            try
            {
                // Attempt failed operation
                cb.Execute(new ThreadStart(FailedOperation));
            }
            catch (OperationFailedException) { }

            Assert.True(CircuitBreakerState.Open == cb.State, "Circuit should be re-opened after failed operation");

            Thread.Sleep(1000);

            Assert.True(CircuitBreakerState.HalfOpen == cb.State, "Circuit should be half-open again after timeout");

            // Attempt successful operation
            cb.Execute(new TestDelegate(ValidOperation), 1, 2);

            Assert.True(CircuitBreakerState.Closed == cb.State, "Circuit should closed after successful operation");
            // **** End of CanCloseBreakerAfterTimeout 
            //**** CanIgnoreExceptionTypes
            cb.IgnoredExceptionTypes.Add(typeof(TimeoutException));

            try
            {
                // Attempt operation
                cb.Execute(new ThreadStart(FailedOperation));
            }
            catch (TimeoutException) { }

            Assert.True(100 == cb.ServiceLevel, "Service level should still be 100%");
            //**** End of CanIgnoreExceptionTypes
            // **** CanRaiseStateChangedEvent
            bool stateChangedEventFired = false;
            cb.StateChanged += (sender, e) =>
            {
                if (cb.State == CircuitBreakerState.Closed)
                {
                    stateChangedEventFired = true;
                }
            };

            cb.Trip();

            Assert.True(CircuitBreakerState.Open == cb.State, "Circuit should be open on trip");

            cb.Reset();

            Assert.True(CircuitBreakerState.Closed == cb.State, "Circuit should closed on reset");
            Assert.True(stateChangedEventFired, "StateChanged event should be fired on reset");

            stateChangedEventFired = false;

            // Reset an already closed circuit
            cb.Reset();

            Assert.False(stateChangedEventFired, "StateChanged event should be only be fired when state changes");
            // **** End of CanRaiseStateChangedEvent
            // **** CanRaiseServiceLevelChangedEvent
            bool serviceLevelChangedEventFired = false;
            cb.ServiceLevelChanged += (sender, e) => { serviceLevelChangedEventFired = true; };

            try
            {
                cb.Execute(new ThreadStart(FailedOperation));
            }
            catch (OperationFailedException) { }

            Assert.True(serviceLevelChangedEventFired, "ServiceLevelChanged event should be fired on failure");
            // **** End of CanRaiseServiceLevelChangedEvent
            // **** CanThrowInvokerException
            Exception verifyException = null;
            try
            {
                // Cause the DynamicInvoke method to throw an exception
                cb.Execute(null);
            }
            catch (Exception ex)
            {
                verifyException = ex;
            }

            Assert.NotNull(verifyException);
            Assert.IsType(typeof(NullReferenceException), verifyException);
            Assert.Null(verifyException.InnerException);
            Assert.True(100 == cb.ServiceLevel, "An invoker exception will not affect the service level");
            // **** End of CanThrowInvokerException
             * */
        }
        public void CanForceTripBreaker()
        {
            // **** CanForceTripBreaker
            bool result = false;
            Assert.True(CircuitBreakerState.Closed == cb.State, "Circuit should be initially closed");

            cb.Trip();

            Assert.True(CircuitBreakerState.Open == cb.State, "Circuit should be open on trip");

            // Calling execute when circuit is tripped should throw an OpenCircuitException
            try
            {
                cb.Execute(new TestDelegate(ValidOperation), 1, 2);
            }
            catch (OpenCircuitException e)
            {
                result = true;
            }
            Assert.True(result);
            // **** End of CanForceTripBreaker
        }

        public void CanCreateCircuitBreaker()
        { 
            SetUp();
            //Threshold should be initialized to 5 failures
            Assert.True(cb.Threshold == 5);
            //Timeout should be initialised to 1 minute
            Assert.True(cb.Timeout == 60000);
            //Level of service should be 100%
            Assert.True(cb.ServiceLevel == 100);
            //IgnoreExceptionTypes should be initialized
            Assert.NotNull(cb.ServiceLevel);
            //IgnoreExceptionTypes list should not contain any values
            Assert.True(cb.IgnoredExceptionTypes.Count == 0);
            //*********** End of Create Circuit Breaker
        }
    }
}
