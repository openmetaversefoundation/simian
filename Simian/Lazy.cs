/*
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Based on MIT licensed code at http://anonsvn.mono-project.com/viewvc/trunk/mcs/class/System.ComponentModel.Composition/src/ComponentModel/System/Lazy.cs
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using System.Threading;

namespace Simian
{
    public class Lazy<T>
    {
        private T m_value = default(T);
        private volatile bool m_isValueCreated;
        private Func<T> m_valueFactory;
        private object m_lock;

        public bool IsValueCreated { get { return m_isValueCreated; } }

        public Lazy()
            : this(() => Activator.CreateInstance<T>())
        {
        }

        public Lazy(bool isThreadSafe)
            : this(() => Activator.CreateInstance<T>(), isThreadSafe)
        {
        }

        public Lazy(Func<T> valueFactory) :
            this(valueFactory, true)
        {
        }

        public Lazy(Func<T> valueFactory, bool isThreadSafe)
        {
            if (isThreadSafe)
                m_lock = new object();

            m_valueFactory = valueFactory;
        }

        public T Value
        {
            get
            {
                if (!m_isValueCreated)
                {
                    if (m_lock != null)
                        Monitor.Enter(m_lock);

                    try
                    {
                        T value = m_valueFactory.Invoke();
                        m_valueFactory = null;
                        Thread.MemoryBarrier();
                        m_value = value;
                        m_isValueCreated = true;
                    }
                    finally
                    {
                        if (m_lock != null)
                            Monitor.Exit(m_lock);
                    }
                }

                return m_value;
            }
        }
    }
}
