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
using System.Collections.Specialized;
using System.Threading;

namespace Simian
{
    /// <summary>
    /// A collection of throttled callback events that allows event compression
    /// and rate limiting
    /// </summary>
    /// <typeparam name="TKey">Key type for tracking and overwriting events</typeparam>
    /// <typeparam name="TValue">Value type for event callbacks</typeparam>
    public class ThrottledQueue<TKey, TValue>
    {
        public delegate void EventCallback(TValue value);

        private OrderedDictionary m_pendingEvents;
        private float m_eventsPerSecond;
        private int m_timerIntervalMS;
        private bool m_running;
        private bool m_flushEvents;
        private int m_lastEventHandler;
        private bool m_timeEvents;
        private EventCallback m_callback;
        private Timer m_timer;
        private int m_leftOverTicks;

        public ThrottledQueue(float eventsPerSecond, int timerIntervalMS, bool timeEvents, EventCallback callback)
        {
            m_pendingEvents = new OrderedDictionary();
            m_eventsPerSecond = eventsPerSecond;
            m_timerIntervalMS = timerIntervalMS;
            m_timeEvents = timeEvents;
            m_callback = callback;
        }

        public ThrottledQueue(float eventsPerSecond, int timerIntervalMS, bool timeEvents, EventCallback callback, int capacity)
        {
            m_pendingEvents = new OrderedDictionary(capacity);
            m_eventsPerSecond = eventsPerSecond;
            m_timerIntervalMS = timerIntervalMS;
            m_timeEvents = timeEvents;
            m_callback = callback;
        }

        public void Start()
        {
            lock (m_pendingEvents)
            {
                m_running = true;
                m_lastEventHandler = Util.TickCount();
                m_timer = new Timer(EventHandler, null, m_timerIntervalMS, Timeout.Infinite);
            }
        }

        public void Stop(bool flushEvents)
        {
            lock (m_pendingEvents)
            {
                m_flushEvents = flushEvents;
                m_running = false;
                m_timer.Dispose();
                m_leftOverTicks = 0;

                EventHandler(null);
            }
        }

        public bool Add(TKey key, TValue value)
        {
            bool hadElement;

            lock (m_pendingEvents)
            {
                hadElement = m_pendingEvents.Contains(key);
                m_pendingEvents[key] = value;
            }

            return !hadElement;
        }

        public bool Remove(TKey key)
        {
            bool hadElement;

            lock (m_pendingEvents)
            {
                hadElement = m_pendingEvents.Contains(key);
                m_pendingEvents.Remove(key);
            }

            return hadElement;
        }

        private void EventHandler(object o)
        {
            List<TValue> values = new List<TValue>();

            lock (m_pendingEvents)
            {
                if (m_running)
                {
                    // Check how much time has passed since the last call
                    int now = Util.TickCount();
                    int timePassed = now - m_lastEventHandler;
                    if (!m_timeEvents)
                        m_lastEventHandler = now;

                    // Calculate the maximum number of events to dequeue
                    m_leftOverTicks += timePassed;
                    int maxEvents = (int)((float)m_leftOverTicks / (m_eventsPerSecond * 1000.0f));
                    m_leftOverTicks -= maxEvents * 1000;

                    int i = 0;
                    while (i++ < maxEvents && m_pendingEvents.Count > 0)
                    {
                        TValue value = (TValue)m_pendingEvents[0];
                        m_pendingEvents.RemoveAt(0);
                        values.Add(value);
                    }
                }
                else if (m_flushEvents)
                {
                    // We're exiting, fire all of the event callbacks
                    while (m_pendingEvents.Count > 0)
                    {
                        TValue value = (TValue)m_pendingEvents[0];
                        m_pendingEvents.RemoveAt(0);
                        values.Add(value);
                    }
                }
                else
                {
                    // We're exiting but event flushing was not requested, dump
                    // all the events
                    m_pendingEvents.Clear();
                }
            }

            // Fire the event callbacks
            if (m_callback != null)
            {
                for (int i = 0; i < values.Count; i++)
                    m_callback(values[i]);
            }

            if (m_timeEvents)
                m_lastEventHandler = Util.TickCount();

            // Start the timer again
            if (m_running)
                m_timer.Change(m_timerIntervalMS, Timeout.Infinite);
        }
    }
}
