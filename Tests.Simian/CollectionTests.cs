/*
 * Copyright (c) Open Metaverse Foundation
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. The name of the author may not be used to endorse or promote products
 *    derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 * THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using Simian;
using NUnit.Framework;
using OpenMetaverse;

namespace Tests.Simian
{
    [TestFixture]
    public class CollectionTests
    {
        #region ThrottledQueue

        [Test]
        [Category("ThrottledQueue")]
        public void ThrottledQueueAddFirstTest()
        {
            Dictionary<int, string> values = new Dictionary<int, string>();
            values[1] = "a";
            values[2] = "b";
            values[3] = "c";

            float eventsPerSecond;
            int timerIntervalMS;
            AutoResetEvent finishedEvent = new AutoResetEvent(false);
            int count = 0;
            
            eventsPerSecond = 1.0f;
            timerIntervalMS = 1000;
            ThrottledQueue<int, string> eq = new ThrottledQueue<int, string>(eventsPerSecond, timerIntervalMS, true,
                delegate(string value)
                {
                    Console.WriteLine("Dequeued: " + value);
                    if (++count == values.Count)
                        finishedEvent.Set();
                }
            );

            foreach (KeyValuePair<int, string> kvp in values)
                eq.Add(kvp.Key, kvp.Value);

            int start = Util.TickCount();
            eq.Start();

            Assert.IsTrue(finishedEvent.WaitOne(5000), "Timed out with " + (values.Count - count) + " pending events");

            int elapsed = Util.TickCount() - start;
            Assert.IsTrue(elapsed >= 3000, "Expected 3000ms to pass, only " + elapsed + " passed");
        }

        [Test]
        [Category("ThrottledQueue")]
        public void ThrottledQueueAddAfterTest()
        {
            Dictionary<int, string> values = new Dictionary<int, string>();
            values[1] = "a";
            values[2] = "b";
            values[3] = "c";

            float eventsPerSecond;
            int timerIntervalMS;
            AutoResetEvent finishedEvent = new AutoResetEvent(false);
            int count = 0;

            eventsPerSecond = 1.0f;
            timerIntervalMS = 1000;
            ThrottledQueue<int, string> eq = new ThrottledQueue<int, string>(eventsPerSecond, timerIntervalMS, true,
                delegate(string value)
                {
                    Console.WriteLine("Dequeued: " + value);
                    if (++count == values.Count)
                        finishedEvent.Set();
                }
            );

            int start = Util.TickCount();
            eq.Start();

            foreach (KeyValuePair<int, string> kvp in values)
                eq.Add(kvp.Key, kvp.Value);

            Assert.IsTrue(finishedEvent.WaitOne(5000), "Timed out with " + (values.Count - count) + " pending events");

            int elapsed = Util.TickCount() - start;
            Assert.IsTrue(elapsed >= 3000, "Expected 3000ms to pass, only " + elapsed + " passed");
        }

        [Test]
        [Category("ThrottledQueue")]
        public void ThrottledQueueFlushTest()
        {
            Dictionary<int, string> values = new Dictionary<int, string>();
            values[1] = "a";
            values[2] = "b";
            values[3] = "c";
            values[4] = "d";
            values[5] = "e";
            values[6] = "f";
            values[7] = "g";

            float eventsPerSecond;
            int timerIntervalMS;
            int count = 0;

            eventsPerSecond = 1.0f;
            timerIntervalMS = 1000;
            ThrottledQueue<int, string> eq = new ThrottledQueue<int, string>(eventsPerSecond, timerIntervalMS, true,
                delegate(string value)
                {
                    Console.WriteLine("Dequeued: " + value);
                    ++count;
                }
            );

            eq.Start();

            foreach (KeyValuePair<int, string> kvp in values)
                eq.Add(kvp.Key, kvp.Value);

            Thread.Sleep(1500);

            eq.Stop(true);

            Assert.IsTrue(count == values.Count, "Attempted flush but only dequeued " + count + " events total");
        }

        [Test]
        [Category("ThrottledQueue")]
        public void ThrottledQueueNoFlushTest()
        {
            Dictionary<int, string> values = new Dictionary<int, string>();
            values[1] = "a";
            values[2] = "b";
            values[3] = "c";
            values[4] = "d";
            values[5] = "e";
            values[6] = "f";
            values[7] = "g";

            float eventsPerSecond;
            int timerIntervalMS;
            int count = 0;

            eventsPerSecond = 1.0f;
            timerIntervalMS = 1000;
            ThrottledQueue<int, string> eq = new ThrottledQueue<int, string>(eventsPerSecond, timerIntervalMS, true,
                delegate(string value)
                {
                    Console.WriteLine("Dequeued: " + value);
                    ++count;
                }
            );

            int start = Util.TickCount();
            eq.Start();

            foreach (KeyValuePair<int, string> kvp in values)
                eq.Add(kvp.Key, kvp.Value);

            Thread.Sleep(1500);

            eq.Stop(false);

            Assert.IsTrue(count == 1, "Attempted to skip flush but instead dequeued " + count + " events total");
        }

        [Test]
        [Category("ThrottledQueue")]
        public void ThrottledQueueSlowTickTest()
        {
            Dictionary<int, string> values = new Dictionary<int, string>();
            values[1] = "a";
            values[2] = "b";
            values[3] = "c";

            float eventsPerSecond;
            int timerIntervalMS;
            AutoResetEvent finishedEvent = new AutoResetEvent(false);
            int count = 0;

            eventsPerSecond = 1.0f;
            timerIntervalMS = 4000;
            ThrottledQueue<int, string> eq = new ThrottledQueue<int, string>(eventsPerSecond, timerIntervalMS, true,
                delegate(string value)
                {
                    Console.WriteLine("Dequeued: " + value);
                    if (++count == values.Count)
                        finishedEvent.Set();
                }
            );

            int start = Util.TickCount();
            eq.Start();

            foreach (KeyValuePair<int, string> kvp in values)
                eq.Add(kvp.Key, kvp.Value);

            Assert.IsTrue(finishedEvent.WaitOne(5000), "Timed out with " + (values.Count - count) + " pending events");

            int elapsed = Util.TickCount() - start;
            Assert.IsTrue(elapsed >= 3000, "Expected 3000ms to pass, only " + elapsed + " passed");
        }

        [Test]
        [Category("ThrottledQueue")]
        public void ThrottledQueueFastTickTest()
        {
            Dictionary<int, string> values = new Dictionary<int, string>();
            values[1] = "a";
            values[2] = "b";
            values[3] = "c";

            float eventsPerSecond;
            int timerIntervalMS;
            AutoResetEvent finishedEvent = new AutoResetEvent(false);
            int count = 0;

            eventsPerSecond = 1.0f;
            timerIntervalMS = 50;
            ThrottledQueue<int, string> eq = new ThrottledQueue<int, string>(eventsPerSecond, timerIntervalMS, true,
                delegate(string value)
                {
                    Console.WriteLine("Dequeued: " + value);
                    if (++count == values.Count)
                        finishedEvent.Set();
                }
            );

            int start = Util.TickCount();
            eq.Start();

            foreach (KeyValuePair<int, string> kvp in values)
                eq.Add(kvp.Key, kvp.Value);

            Assert.IsTrue(finishedEvent.WaitOne(5000), "Timed out with " + (values.Count - count) + " pending events");

            int elapsed = Util.TickCount() - start;
            Assert.IsTrue(elapsed >= 3000, "Expected 3000ms to pass, only " + elapsed + " passed");
        }

        [Test]
        [Category("ThrottledQueue")]
        public void ThrottledQueueSlowEventsTest()
        {
            Dictionary<int, string> values = new Dictionary<int, string>();
            values[1] = "a";
            values[2] = "b";
            values[3] = "c";
            values[4] = "d";
            values[5] = "e";

            float eventsPerSecond;
            int timerIntervalMS;
            AutoResetEvent finishedEvent = new AutoResetEvent(false);
            int count = 0;

            eventsPerSecond = 0.25f;
            timerIntervalMS = 1000;
            ThrottledQueue<int, string> eq = new ThrottledQueue<int, string>(eventsPerSecond, timerIntervalMS, true,
                delegate(string value)
                {
                    Console.WriteLine("Dequeued: " + value);
                    if (++count == values.Count)
                        finishedEvent.Set();
                }
            );

            int start = Util.TickCount();
            eq.Start();

            foreach (KeyValuePair<int, string> kvp in values)
                eq.Add(kvp.Key, kvp.Value);

            Assert.IsTrue(finishedEvent.WaitOne(4000), "Timed out with " + (values.Count - count) + " pending events");

            int elapsed = Util.TickCount() - start;
            Assert.IsTrue(elapsed >= 2000, "Expected 2000ms to pass, only " + elapsed + " passed");
        }

        #endregion ThrottledQueue
    }
}
