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
using System.Net;
using System.Threading;
using HttpServer;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian.Protocols.Linden
{
    public class LLEventQueueEvent
    {
        public string Name;
        public OSDMap Body;

        public LLEventQueueEvent(string name, OSDMap body)
        {
            Name = name;
            Body = body;
        }
    }

    public class LLEventQueue : IDisposable
    {
        /// <summary>This interval defines the amount of time to wait, in milliseconds,
        /// for new events to show up on the queue before sending a response to the 
        /// client and completing the HTTP request. The interval also specifies the 
        /// maximum time that can pass before the queue shuts down after Stop() or the
        /// class destructor is called</summary>
        const int BATCH_WAIT_INTERVAL = 200;

        /// <summary>Since multiple events can be batched together and sent in the same
        /// response, this prevents the event queue thread from infinitely dequeueing 
        /// events and never sending a response if there is a constant stream of new 
        /// events</summary>
        const int MAX_EVENTS_PER_RESPONSE = 5;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        public BlockingQueue<LLEventQueueEvent> EventQueue;
        public int CurrentID;
        public bool ConnectionOpen;
        public IHttpClientContext Context;
        public IHttpRequest Request;
        public IHttpResponse Response;
        public int StartTime;

        public LLEventQueue()
        {
            EventQueue = new BlockingQueue<LLEventQueueEvent>();
            CurrentID = 1;
        }

        public void QueueEvent(string eventName, OSDMap body)
        {
            EventQueue.Enqueue(new LLEventQueueEvent(eventName, body));
        }

        public void SendEvents()
        {
            SendEvents(BATCH_WAIT_INTERVAL);
        }

        public void SendEvents(object o)
        {
            SendEvents(BATCH_WAIT_INTERVAL);
        }

        public void SendEvents(int batchWaitMS)
        {
            if (Response == null)
                return;

            LLEventQueueEvent eventQueueEvent = null;

            if (EventQueue.Dequeue(batchWaitMS, ref eventQueueEvent))
            {
                // An event was dequeued
                List<LLEventQueueEvent> eventsToSend = null;

                if (eventQueueEvent != null)
                {
                    eventsToSend = new List<LLEventQueueEvent>();
                    eventsToSend.Add(eventQueueEvent);

                    int start = Util.TickCount();
                    int batchMsPassed = 0;

                    // Wait batchWaitMS milliseconds looking for more events,
                    // or until the size of the current batch equals MAX_EVENTS_PER_RESPONSE
                    while (batchMsPassed < batchWaitMS && eventsToSend.Count < MAX_EVENTS_PER_RESPONSE)
                    {
                        if (EventQueue.Dequeue(batchWaitMS - batchMsPassed, ref eventQueueEvent) && eventQueueEvent != null)
                            eventsToSend.Add(eventQueueEvent);

                        batchMsPassed = Util.TickCount() - start;
                    }
                }

                // Make sure we can actually send the events right now
                if (Response != null && !Response.Sent)
                {
                    SendResponse(eventsToSend);
                }
                else
                {
                    m_log.Info("Connection is closed, requeuing events and closing the handler thread");
                    if (eventsToSend != null)
                    {
                        for (int i = 0; i < eventsToSend.Count; i++)
                            EventQueue.Enqueue(eventsToSend[i]);
                    }
                }
            }
        }

        public void Dispose()
        {
            SendEvents();

            if (Context != null)
            {
                try { Context.Disconnect(System.Net.Sockets.SocketError.Shutdown); }
                catch { }
            }

            Context = null;
            Request = null;
            Response = null;
        }

        public void SendResponse(List<LLEventQueueEvent> eventsToSend)
        {
            if (Request == null || Response == null)
            {
                ConnectionOpen = false;
                m_log.Warn("Cannot send response, connection is closed");
                return;
            }

            Response.Connection = Request.Connection;

            if (eventsToSend != null)
            {
                OSDArray responseArray = new OSDArray(eventsToSend.Count);

                // Put all of the events in an array
                for (int i = 0; i < eventsToSend.Count; i++)
                {
                    LLEventQueueEvent currentEvent = eventsToSend[i];

                    OSDMap eventMap = new OSDMap(2);
                    eventMap.Add("message", OSD.FromString(currentEvent.Name));
                    eventMap.Add("body", currentEvent.Body);
                    responseArray.Add(eventMap);
                }

                // Create a map containing the events array and the id of this response
                OSDMap responseMap = new OSDMap(2);
                responseMap.Add("events", responseArray);
                responseMap.Add("id", OSD.FromInteger(CurrentID++));

                // Serialize the events and send the response
                string responseBody = OSDParser.SerializeLLSDXmlString(responseMap);

                m_log.Debug("Sending " + responseArray.Count + " events over the event queue");
                Context.Respond(HttpHelper.HTTP11, HttpStatusCode.OK, "OK", responseBody, "application/xml");
            }
            else
            {
                //m_log.Debug("Sending a timeout response over the event queue");

                // The 502 response started as a bug in the LL event queue server implementation,
                // but is now hardcoded into the protocol as the code to use for a timeout
                Context.Respond(HttpHelper.HTTP10, HttpStatusCode.BadGateway, "Upstream error:", "Upstream error:", null);
            }

            ConnectionOpen = false;
        }
    }
}
