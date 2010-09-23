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
using System.Threading;

namespace Simian
{
    /// <summary>
    /// The method used by Util.FireAndForget for asynchronously firing events
    /// </summary>
    public enum FireAndForgetMethod
    {
        UnsafeQueueUserWorkItem,
        QueueUserWorkItem,
        BeginInvoke,
        Thread,
    }

    public class WatchdogTimeoutArgs : EventArgs
    {
        public Thread Thread;
        public int LastTick;
    }

    public interface IScheduler
    {
        /// <summary>This event is called whenever a tracked thread is
        /// stopped or has not called UpdateThread() in time</summary>
        event EventHandler<WatchdogTimeoutArgs> OnWatchdogTimeout;

        FireAndForgetMethod FireAndForgetMethod { get; set; }

        /// <summary>
        /// Start a new thread that is tracked by the watchdog timer
        /// </summary>
        /// <param name="start">The method that will be executed in a new thread</param>
        /// <param name="name">A name to give to the new thread</param>
        /// <param name="priority">Priority to run the thread at</param>
        /// <param name="isBackground">True to run this thread as a background
        /// thread, otherwise false</param>
        /// <returns>The newly created Thread object</returns>
        Thread StartThread(ThreadStart start, string name, ThreadPriority priority, bool isBackground);
        /// <summary>
        /// Marks the current thread as alive
        /// </summary>
        void ThreadKeepAlive();
        /// <summary>
        /// Stops watchdog tracking on the current thread
        /// </summary>
        /// <returns>True if the thread was removed from the list of tracked
        /// threads, otherwise false</returns>
        bool RemoveThread();

        /// <summary>
        /// Fires a method asynchronously where no return value or indication
        /// that the method has finished is necessary
        /// </summary>
        /// <param name="callback">Method to execute asynchronously</param>
        /// <param name="obj">Object to pass to the asynchronous method</param>
        void FireAndForget(WaitCallback callback, object obj);

        /// <summary>
        /// Gets the number of available workers for handling FireAndForget tasks
        /// </summary>
        /// <returns>The number of available workers</returns>
        int GetAvailableWorkers();
    }
}
