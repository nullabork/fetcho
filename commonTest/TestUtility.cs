using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;

namespace Fetcho.Common.Tests
{
    public static class TestUtility
    {
        /// <summary>
        /// Tests if a piece of code takes longer than a specified threshold
        /// </summary>
        /// <param name="action"></param>
        /// <param name="maximumExecutionTimeInMilliseconds"></param>
        public static void AssertExecutionTimeIsLessThan(Action action, int maximumExecutionTimeInMilliseconds)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            action.Invoke();
            watch.Stop();
            Assert.IsTrue(
                watch.ElapsedMilliseconds <= maximumExecutionTimeInMilliseconds,
                "Execution time was greater than the maximum threshold at {0}ms", 
                watch.ElapsedMilliseconds);            
        }

        /// <summary>
        /// Tests if a piece of code takes less than a specified threshold
        /// </summary>
        /// <param name="action"></param>
        /// <param name="maximumExecutionTimeInMilliseconds"></param>
        public static void AssertExecutionTimeIsGreaterThan(Action action, int minimumExecutionTimeInMilliseconds)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            action.Invoke();
            watch.Stop();
            Assert.IsTrue(
                watch.ElapsedMilliseconds >= minimumExecutionTimeInMilliseconds, 
                "Execution time was less than the minimum threshold at {0}ms",
                watch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Tests if a piece of code executes within a specified range
        /// </summary>
        /// <param name="action"></param>
        /// <param name="maximumExecutionTimeInMilliseconds"></param>
        public static void AssertExecutionTimeIsInTheRange(Action action, int minimumExecutionTimeInMilliseconds, int maximumExecutionTimeInMilliseconds)
        {
            Stopwatch watch = new Stopwatch(); 
            watch.Start();
            action.Invoke();
            watch.Stop();
            Assert.IsTrue(
                watch.ElapsedMilliseconds >= minimumExecutionTimeInMilliseconds, 
                "Execution time was less than the minimum threshold at {0}ms", 
                watch.ElapsedMilliseconds);
            Assert.IsTrue(
                watch.ElapsedMilliseconds <= maximumExecutionTimeInMilliseconds, 
                "Execution time was greater than the maximum threshold at {0}ms", 
                watch.ElapsedMilliseconds);
        }
    }
}
