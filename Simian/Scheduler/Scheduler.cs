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
using System.ComponentModel.Composition;
using System.Threading;
using log4net;

namespace Simian
{
    #region Singleton

    /// <summary>
    /// Used for classes that are single instances per appdomain
    /// </summary>
    public static class Singleton
    {
        private static class Storage<T>
        {
            internal static T s_instance;
        }

        public static T GetInstance<T>(Func<T> op)
        {
            if (Storage<T>.s_instance == null)
            {
                lock (typeof(Storage<T>))
                {
                    if (Storage<T>.s_instance == null)
                    {
                        T temp = op();
                        System.Threading.Thread.MemoryBarrier();
                        Storage<T>.s_instance = temp;
                    }
                }
            }
            return Storage<T>.s_instance;
        }

        public static T GetInstance<T>()
            where T : new()
        {
            return GetInstance(() => new T());
        }
    }

    #endregion

    [ApplicationModule("Scheduler")]
    public class Scheduler : IScheduler, IApplicationModule
    {
        /// <summary>Timer interval in milliseconds for the watchdog timer</summary>
        const double WATCHDOG_INTERVAL_MS = 2500.0d;
        /// <summary>Maximum timeout in milliseconds before a thread is considered dead</summary>
        const int WATCHDOG_TIMEOUT_MS = 5000;

        [System.Diagnostics.DebuggerDisplay("{Thread.Name}")]
        private class ThreadWatchdogInfo
        {
            public Thread Thread;
            public int LastTick;

            public ThreadWatchdogInfo(Thread thread)
            {
                Thread = thread;
                LastTick = Environment.TickCount & Int32.MaxValue;
            }
        }

        /// <summary>This event is called whenever a tracked thread is
        /// stopped or has not called UpdateThread() in time</summary>
        public event EventHandler<WatchdogTimeoutArgs> OnWatchdogTimeout;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);
        private Dictionary<int, ThreadWatchdogInfo> m_threads;
        private System.Timers.Timer m_watchdogTimer;

        public FireAndForgetMethod FireAndForgetMethod { get; set; }

        public Scheduler()
        {
        }

        public bool Start(Simian simian)
        {
            m_threads = new Dictionary<int, ThreadWatchdogInfo>();
            m_watchdogTimer = new System.Timers.Timer(WATCHDOG_INTERVAL_MS);
            m_watchdogTimer.AutoReset = false;
            m_watchdogTimer.Elapsed += WatchdogTimerElapsed;
            m_watchdogTimer.Start();

            return true;
        }

        public void Stop()
        {
            m_watchdogTimer.Stop();
            m_watchdogTimer.Close();
        }

        public Thread StartThread(ThreadStart start, string name, ThreadPriority priority, bool isBackground)
        {
            Thread thread = new Thread(start);
            thread.Name = name;
            thread.Priority = priority;
            thread.IsBackground = isBackground;
            thread.Start();

            return thread;
        }

        public void ThreadKeepAlive()
        {
            ThreadWatchdogInfo threadInfo;

            // Although TryGetValue is not a thread safe operation, we use a try/catch here instead
            // of a lock for speed. Adding/removing threads is a very rare operation compared to
            // UpdateThread(), and a single UpdateThread() failure here and there won't break
            // anything
            try
            {
                if (m_threads.TryGetValue(Thread.CurrentThread.ManagedThreadId, out threadInfo))
                    threadInfo.LastTick = Environment.TickCount & Int32.MaxValue;
                else
                    AddThread(new ThreadWatchdogInfo(Thread.CurrentThread));
            }
            catch { }
        }

        public bool RemoveThread()
        {
            lock (m_threads)
                return m_threads.Remove(Thread.CurrentThread.ManagedThreadId);
        }

        #region FireAndForget Pattern

        public void FireAndForget(WaitCallback callback, object obj)
        {
            switch (FireAndForgetMethod)
            {
                case FireAndForgetMethod.UnsafeQueueUserWorkItem:
                    ThreadPool.UnsafeQueueUserWorkItem(callback, obj);
                    break;
                case FireAndForgetMethod.QueueUserWorkItem:
                    ThreadPool.QueueUserWorkItem(callback, obj);
                    break;
                case FireAndForgetMethod.BeginInvoke:
                    FireAndForgetWrapper wrapper = Singleton.GetInstance<FireAndForgetWrapper>();
                    wrapper.FireAndForget(callback, obj);
                    break;
                case FireAndForgetMethod.Thread:
                    Thread thread = new Thread(delegate(object o) { callback(o); });
                    thread.Start(obj);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public int GetAvailableWorkers()
        {
            const int MAX_SYSTEM_THREADS = 200;

            switch (FireAndForgetMethod)
            {
                case FireAndForgetMethod.UnsafeQueueUserWorkItem:
                case FireAndForgetMethod.QueueUserWorkItem:
                case FireAndForgetMethod.BeginInvoke:
                    int workerThreads, iocpThreads;
                    ThreadPool.GetAvailableThreads(out workerThreads, out iocpThreads);
                    return workerThreads;
                case FireAndForgetMethod.Thread:
                    return MAX_SYSTEM_THREADS - System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Created to work around a limitation in Mono with nested delegates
        /// </summary>
        private class FireAndForgetWrapper
        {
            public void FireAndForget(System.Threading.WaitCallback callback)
            {
                callback.BeginInvoke(null, EndFireAndForget, callback);
            }

            public void FireAndForget(System.Threading.WaitCallback callback, object obj)
            {
                callback.BeginInvoke(obj, EndFireAndForget, callback);
            }

            private static void EndFireAndForget(IAsyncResult ar)
            {
                System.Threading.WaitCallback callback = (System.Threading.WaitCallback)ar.AsyncState;

                try { callback.EndInvoke(ar); }
                catch (Exception ex) { m_log.Error("[UTIL]: Asynchronous method threw an exception: " + ex.Message, ex); }

                ar.AsyncWaitHandle.Close();
            }
        }

        #endregion FireAndForget Pattern

        private void AddThread(ThreadWatchdogInfo threadInfo)
        {
            m_log.Debug("[WATCHDOG]: Started tracking thread \"" + threadInfo.Thread.Name + "\" (ID " + threadInfo.Thread.ManagedThreadId + ")");

            lock (m_threads)
                m_threads.Add(threadInfo.Thread.ManagedThreadId, threadInfo);
        }

        private void WatchdogTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            EventHandler<WatchdogTimeoutArgs> callback = OnWatchdogTimeout;

            if (callback != null)
            {
                ThreadWatchdogInfo timedOut = null;

                lock (m_threads)
                {
                    int now = Environment.TickCount & Int32.MaxValue;

                    foreach (ThreadWatchdogInfo threadInfo in m_threads.Values)
                    {
                        if (threadInfo.Thread.ThreadState == ThreadState.Stopped || now - threadInfo.LastTick >= WATCHDOG_TIMEOUT_MS)
                        {
                            timedOut = threadInfo;
                            m_threads.Remove(threadInfo.Thread.ManagedThreadId);
                            break;
                        }
                    }
                }

                if (timedOut != null)
                    callback(this, new WatchdogTimeoutArgs { Thread = timedOut.Thread, LastTick = timedOut.LastTick });
            }

            m_watchdogTimer.Start();
        }
    }
}
