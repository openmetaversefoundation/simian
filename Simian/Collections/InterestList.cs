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
using OpenMetaverse;
using log4net;

namespace Simian
{
    #region Delegates

    /// <summary>
    /// Prioritizes or prunes an event. Lower numbers indicate a higher
    /// priority
    /// </summary>
    /// <remarks>The default prioritizer uses the squared distance between a 
    /// recipient and the event in question. Negative numbers are also allowed,
    /// although the distance prioritizer cannot generate them</remarks>
    /// <param name="eventData">Event that needs prioritization</param>
    /// <param name="presence">Scene presence this event is being prioritized for</param>
    /// <returns>A double-precision float representing the priority of this 
    /// event (lower value means higher priority), or null if this event should
    /// be suppressed entirely</returns>
    public delegate double? PrioritizeEventCallback(InterestListEvent eventData, IScenePresence presence);

    /// <summary>
    /// Combines two events of the same type together
    /// </summary>
    /// <remarks>The default combiner will overwrite the old event with the new
    /// one</remarks>
    /// <param name="currentData">Existing event</param>
    /// <param name="newData">New event with an ID matching the existing event</param>
    /// <returns>The event that will replace currentData</returns>
    public delegate QueuedInterestListEvent CombineEventsCallback(QueuedInterestListEvent currentData, QueuedInterestListEvent newData);

    /// <summary>
    /// Called when one or more events of a single type are dequeued from the 
    /// interest list
    /// </summary>
    /// <remarks>The default sender does nothing. This should always be
    /// implemented for every event</remarks>
    /// <param name="eventDatas">An array containing the dequeued events</param>
    /// <param name="presence">Scene presence to send the events to</param>
    public delegate void SendEventCallback(QueuedInterestListEvent[] eventDatas, IScenePresence presence);

    #endregion Delegates

    #region Event Classes

    /// <summary>
    /// Holds information about an event that happened in a scene. This class
    /// is intended to be generic and can represent anything from an entity 
    /// changing position to a chat message
    /// </summary>
    public sealed class InterestListEvent
    {
        /// <summary>Unique ID of this event. If another event is generated 
        /// with this same ID, a <seealso cref="CombineEventsCallback"/> event 
        /// will be used to combine them</summary>
        public readonly UUID ID;
        /// <summary>A free-form string identifying the type of this event</summary>
        public readonly string Type;
        /// <summary>Scene-relative location where this event occurred</summary>
        public readonly Vector3 ScenePosition;
        /// <summary>Size of the event. This may be used by some 
        /// <seealso cref="PrioritizeEventCallback"/> implementations. In 
        /// events where it has no meaning use Vector3.One</summary>
        public readonly Vector3 Scale;
        /// <summary>The actual event data</summary>
        public readonly object State;

        /// <summary>
        /// Default constructor
        /// </summary>
        public InterestListEvent(UUID id, string type, Vector3 scenePosition, Vector3 scale, object state)
        {
            ID = id;
            Type = type;
            ScenePosition = scenePosition;
            Scale = scale;
            State = state;
        }
    }

    /// <summary>
    /// A wrapper around the three callbacks used for a type of interest list event
    /// </summary>
    public sealed class InterestListEventHandler
    {
        /// <summary>Prioritizer</summary>
        public PrioritizeEventCallback PriorityCallback = DefaultPrioritizer;
        /// <summary>Combiner</summary>
        public CombineEventsCallback CombineCallback = DefaultCombiner;
        /// <summary>Sender</summary>
        public SendEventCallback SendCallback = DefaultSender;

        /// <summary>
        /// The default prioritizer for interest list events. Returns the 
        /// squared distance between the event and the target presence
        /// </summary>
        public static double? DefaultPrioritizer(InterestListEvent eventData, IScenePresence presence)
        {
            // A simple distance-based prioritizer
            return Vector3.DistanceSquared(presence.ScenePosition, eventData.ScenePosition);
        }

        /// <summary>
        /// The default combiner for interest list events. Returns newData
        /// </summary>
        private static QueuedInterestListEvent DefaultCombiner(QueuedInterestListEvent currentData, QueuedInterestListEvent newData)
        {
            // Default behavior is to overwrite old events with new ones
            return newData;
        }

        /// <summary>
        /// The default sender for interest list events. Does nothing
        /// </summary>
        private static void DefaultSender(QueuedInterestListEvent[] eventDatas, IScenePresence presence)
        {
            // Default is no-op
        }
    }

    /// <summary>
    /// An event that has been prioritized and inserted into an interest list
    /// </summary>
    public sealed class QueuedInterestListEvent : IComparable<QueuedInterestListEvent>
    {
        /// <summary>Event that has been assigned handlers and a priority</summary>
        public InterestListEvent Event;
        /// <summary>Computed priority of this event. Lower means a higher 
        /// priority</summary>
        public double Priority;
        /// <summary>A reference to the callbacks that handle this event</summary>
        public InterestListEventHandler Handler;

        /// <summary>
        /// Default constructor
        /// </summary>
        public QueuedInterestListEvent(InterestListEvent eventData, double priority, InterestListEventHandler handler)
        {
            Event = eventData;
            Priority = priority;
            Handler = handler;
        }

        /// <summary>
        /// Comparison function based on the Priority field
        /// </summary>
        public int CompareTo(QueuedInterestListEvent ped)
        {
            return Priority.CompareTo(ped.Priority);
        }
    }

    #endregion Event Classes

    /// <summary>
    /// A prioritized list of events that need to be sent to a scene presence
    /// </summary>
    public class InterestList : IInterestList
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        // The scene presence this interest list belongs to
        private IScenePresence m_presence;
        // Provides fast event lookups by ID
        private Dictionary<UUID, C5.PriorityQueueHandle> m_eventIDs;
        // Prioritizes the events
        private C5.IntervalHeap<QueuedInterestListEvent> m_eventHeap;
        // The comparer used by the priority queue
        private C5.NaturalComparer<QueuedInterestListEvent> m_comparer = new C5.NaturalComparer<QueuedInterestListEvent>();
        // Used for locking
        private object m_syncRoot = new object();

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="presence">The scene presence this interest list belongs to</param>
        public InterestList(IScenePresence presence, int initialCapacity)
        {
            m_presence = presence;
            m_eventIDs = new Dictionary<UUID, C5.PriorityQueueHandle>(initialCapacity);
            m_eventHeap = new C5.IntervalHeap<QueuedInterestListEvent>(initialCapacity, m_comparer);
        }

        /// <summary>
        /// Adds an event to the interest list
        /// </summary>
        /// <param name="eventData">Event to add</param>
        /// <param name="handler">Collection of callbacks that will handle this
        /// event</param>
        public void EnqueueEvent(InterestListEvent eventData, InterestListEventHandler handler)
        {
            double? priority = handler.PriorityCallback(eventData, m_presence);

            // If this event has a non-null priority for this presence, enqueue it
            if (priority.HasValue)
            {
                QueuedInterestListEvent qile = new QueuedInterestListEvent(eventData, priority.Value, handler);

                lock (m_syncRoot)
                {
                    C5.PriorityQueueHandle handle;
                    if (m_eventIDs.TryGetValue(eventData.ID, out handle))
                    {
                        // An event with the same ID already exists in the priority queue. Combine this update with the previous one
                        qile = handler.CombineCallback(m_eventHeap[handle], qile);
                        m_eventHeap.Replace(handle, qile);
                    }
                    else
                    {
                        // This event ID is new
                        m_eventHeap.Add(ref handle, qile);
                        m_eventIDs.Add(eventData.ID, handle);
                    }
                }
            }
        }

        /// <summary>
        /// Dequeues the highest priority events
        /// </summary>
        /// <param name="count">Maximum number of events to dequeue</param>
        public void DequeueEvents(int count)
        {
            Dictionary<string, List<QueuedInterestListEvent>> dequeued = new Dictionary<string, List<QueuedInterestListEvent>>();
            List<QueuedInterestListEvent> events;

            // Fetch the requested number of events, or as many as are available.
            // Put them in collections sorted by event type
            lock (m_syncRoot)
            {
                while (!m_eventHeap.IsEmpty && count-- > 0)
                {
                    QueuedInterestListEvent qile = m_eventHeap.DeleteMin();
                    m_eventIDs.Remove(qile.Event.ID);

                    if (!dequeued.TryGetValue(qile.Event.Type, out events))
                    {
                        events = new List<QueuedInterestListEvent>();
                        dequeued.Add(qile.Event.Type, events);
                    }

                    events.Add(qile);
                }
            }

            // Fire a SendEventCallback for each unique event type
            foreach (List<QueuedInterestListEvent> eventsList in dequeued.Values)
            {
                QueuedInterestListEvent[] eventsArray = eventsList.ToArray();
                InterestListEventHandler handler = eventsArray[0].Handler;

                handler.SendCallback(eventsArray, m_presence);
            }
        }

        /// <summary>
        /// Reprioritize all of the currently queued events
        /// </summary>
        public void Reprioritize()
        {
            lock (m_syncRoot)
            {
                List<QueuedInterestListEvent> removedEvents = new List<QueuedInterestListEvent>(0);

                foreach (C5.PriorityQueueHandle handle in m_eventIDs.Values)
                {
                    QueuedInterestListEvent qile = m_eventHeap[handle];
                    double? priority = qile.Handler.PriorityCallback(qile.Event, m_presence);

                    if (priority.HasValue)
                    {
                        // Update the event with its new priority and reinsert
                        qile.Priority = priority.Value;
                        m_eventHeap.Replace(handle, qile);
                    }
                    else
                    {
                        // Delete this event from the priority queue and mark 
                        // it for removal from the dictionary
                        m_eventHeap.Delete(handle);
                        removedEvents.Add(qile);
                    }
                }

                // Erase any removed events from the m_eventIDs dictionary
                for (int i = 0; i < removedEvents.Count; i++)
                    m_eventIDs.Remove(removedEvents[i].Event.ID);
            }
        }
    }
}
