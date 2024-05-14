using NUnit.Framework;
using O2DESNet.Demos;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace O2DESNet.UnitTests
{
    public class ListSort_Tests
    {
        private void f1() 
        {
            Console.WriteLine("f1");
        }
        [Test]
        public void Test1()
        {
            Sandbox sandbox = new Sandbox();
            DateTime dateTime1 = DateTime.Now;
            DateTime dateTime2 = dateTime1.AddSeconds(-1);
            DateTime dateTime3 = dateTime1.AddSeconds(1);
            sandbox.AddWithOrder(f1, dateTime1, "dt1");
            sandbox.AddWithOrder(f1, dateTime2, "dt2");
            sandbox.AddWithOrder(f1, dateTime3, "dt3");
            string result = sandbox.PrintFEL();
            if (result == "dt2dt1dt3") 
                Assert.Pass();
            else
                Assert.Fail();
        }
    }
}
