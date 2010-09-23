/*
 Copyright (c) 2003-2006 Niels Kokholm and Peter Sestoft
 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:
 
 The above copyright notice and this permission notice shall be included in
 all copies or substantial portions of the Software.
 
 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 SOFTWARE.
*/

using System;
using SCG = System.Collections.Generic;

namespace C5
{
    /// <summary>
    /// A handle to an element in a priority queue (interval heap)
    /// </summary>
    public sealed class PriorityQueueHandle
    {
        /// <summary>
        /// To save space, the index is 2*cell for heap[cell].first, and 2*cell+1 for heap[cell].last
        /// </summary>
        internal int index = -1;

        public override string ToString()
        {
            return String.Format("[{0}]", index);
        }
    }

    /// <summary>
    /// A natural generic IComparer for an IComparable&lt;T&gt; item type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class NaturalComparer<T> : SCG.IComparer<T>
        where T : IComparable<T>
    {
        /// <summary>
        /// Compare two items
        /// </summary>
        /// <param name="item1">First item</param>
        /// <param name="item2">Second item</param>
        /// <returns>item1 &lt;=&gt; item2</returns>
        public int Compare(T item1, T item2) { return item1 != null ? item1.CompareTo(item2) : item2 != null ? -1 : 0; }
    }

    /// <summary>
    /// A priority queue class based on an interval heap data structure.
    /// </summary>
    /// <typeparam name="T">The item type</typeparam>
    public sealed class IntervalHeap<T>
    {
        #region Classes

        /// <summary>
        /// An equalityComparer compatible with a given comparer. All hash codes are 0, 
        /// meaning that anything based on hash codes will be quite inefficient.
        /// <para><b>Note: this will give a new EqualityComparer each time created!</b></para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private sealed class ComparerZeroHashCodeEqualityComparer : SCG.IEqualityComparer<T>
        {
            SCG.IComparer<T> comparer;
            /// <summary>
            /// Create a trivial <see cref="T:C5.IEqualityComparer`1"/> compatible with the 
            /// <see cref="T:C5.IComparer`1"/> <code>comparer</code>
            /// </summary>
            /// <param name="comparer"></param>
            public ComparerZeroHashCodeEqualityComparer(SCG.IComparer<T> comparer)
            {
                if (comparer == null)
                    throw new NullReferenceException("Comparer cannot be null");
                this.comparer = comparer;
            }
            /// <summary>
            /// A trivial, inefficient hash fuction. Compatible with any equality relation.
            /// </summary>
            /// <param name="item"></param>
            /// <returns>0</returns>
            public int GetHashCode(T item) { return 0; }
            /// <summary>
            /// Equality of two items as defined by the comparer.
            /// </summary>
            /// <param name="item1"></param>
            /// <param name="item2"></param>
            /// <returns></returns>
            public bool Equals(T item1, T item2) { return comparer.Compare(item1, item2) == 0; }
        }

        struct Interval
        {
            internal T first, last; internal PriorityQueueHandle firsthandle, lasthandle;

            public override string ToString() { return String.Format("[{0}; {1}]", first, last); }
        }

        #endregion

        #region Fields
        
        SCG.IComparer<T> comparer;
        SCG.IEqualityComparer<T> itemequalityComparer;
        Interval[] heap;
        int size;

        #endregion

        #region Util
        bool heapifyMin(int i)
        {
            bool swappedroot = false;
            int cell = i, currentmin = cell;
            T currentitem = heap[cell].first;
            PriorityQueueHandle currenthandle = heap[cell].firsthandle;

            if (i > 0)
            {
                T other = heap[cell].last;
                if (2 * cell + 1 < size && comparer.Compare(currentitem, other) > 0)
                {
                    swappedroot = true;
                    PriorityQueueHandle otherhandle = heap[cell].lasthandle;
                    updateLast(cell, currentitem, currenthandle);
                    currentitem = other;
                    currenthandle = otherhandle;
                }
            }

            T minitem = currentitem;
            PriorityQueueHandle minhandle = currenthandle;

            while (true)
            {
                int l = 2 * cell + 1, r = l + 1;
                T lv, rv;

                if (2 * l < size && comparer.Compare(lv = heap[l].first, minitem) < 0)
                { currentmin = l; minitem = lv; }

                if (2 * r < size && comparer.Compare(rv = heap[r].first, minitem) < 0)
                { currentmin = r; minitem = rv; }

                if (currentmin == cell)
                    break;

                minhandle = heap[currentmin].firsthandle;
                updateFirst(cell, minitem, minhandle);
                cell = currentmin;

                //Maybe swap first and last
                T other = heap[cell].last;
                if (2 * currentmin + 1 < size && comparer.Compare(currentitem, other) > 0)
                {
                    PriorityQueueHandle otherhandle = heap[cell].lasthandle;
                    updateLast(cell, currentitem, currenthandle);
                    currentitem = other;
                    currenthandle = otherhandle;
                }


                minitem = currentitem;
                minhandle = currenthandle;
            }

            if (cell != i || swappedroot)
                updateFirst(cell, minitem, minhandle);
            return swappedroot;
        }

        bool heapifyMax(int i)
        {
            bool swappedroot = false;
            int cell = i, currentmax = cell;
            T currentitem = heap[cell].last;
            PriorityQueueHandle currenthandle = heap[cell].lasthandle;

            if (i > 0)
            {
                T other = heap[cell].first;
                if (comparer.Compare(currentitem, other) < 0)
                {
                    swappedroot = true;
                    PriorityQueueHandle otherhandle = heap[cell].firsthandle;
                    updateFirst(cell, currentitem, currenthandle);
                    currentitem = other;
                    currenthandle = otherhandle;
                }
            }

            T maxitem = currentitem;
            PriorityQueueHandle maxhandle = currenthandle;

            while (true)
            {
                int l = 2 * cell + 1, r = l + 1;
                T lv, rv;

                if (2 * l + 1 < size && comparer.Compare(lv = heap[l].last, maxitem) > 0)
                { currentmax = l; maxitem = lv; }

                if (2 * r + 1 < size && comparer.Compare(rv = heap[r].last, maxitem) > 0)
                { currentmax = r; maxitem = rv; }

                if (currentmax == cell)
                    break;

                maxhandle = heap[currentmax].lasthandle;
                updateLast(cell, maxitem, maxhandle);
                cell = currentmax;

                //Maybe swap first and last
                T other = heap[cell].first;
                if (comparer.Compare(currentitem, other) < 0)
                {
                    PriorityQueueHandle otherhandle = heap[cell].firsthandle;
                    updateFirst(cell, currentitem, currenthandle);
                    currentitem = other;
                    currenthandle = otherhandle;
                }

                maxitem = currentitem;
                maxhandle = currenthandle;
            }

            if (cell != i || swappedroot) //Check could be better?
                updateLast(cell, maxitem, maxhandle);
            return swappedroot;
        }

        void bubbleUpMin(int i)
        {
            if (i > 0)
            {
                T min = heap[i].first, iv = min;
                PriorityQueueHandle minhandle = heap[i].firsthandle;
                int p = (i + 1) / 2 - 1;

                while (i > 0)
                {
                    if (comparer.Compare(iv, min = heap[p = (i + 1) / 2 - 1].first) < 0)
                    {
                        updateFirst(i, min, heap[p].firsthandle);
                        min = iv;
                        i = p;
                    }
                    else
                        break;
                }

                updateFirst(i, iv, minhandle);
            }
        }

        void bubbleUpMax(int i)
        {
            if (i > 0)
            {
                T max = heap[i].last, iv = max;
                PriorityQueueHandle maxhandle = heap[i].lasthandle;
                int p = (i + 1) / 2 - 1;

                while (i > 0)
                {
                    if (comparer.Compare(iv, max = heap[p = (i + 1) / 2 - 1].last) > 0)
                    {
                        updateLast(i, max, heap[p].lasthandle);
                        max = iv;
                        i = p;
                    }
                    else
                        break;
                }

                updateLast(i, iv, maxhandle);

            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Create an interval heap with external item comparer and default initial capacity (16)
        /// </summary>
        /// <param name="comparer">The external comparer</param>
        public IntervalHeap(SCG.IComparer<T> comparer) : this(16, comparer) { }

        /// <summary>
        /// Create an interval heap with external item comparer and prescribed initial capacity
        /// </summary>
        /// <param name="comparer">The external comparer</param>
        /// <param name="capacity">The initial capacity</param>
        public IntervalHeap(int capacity, SCG.IComparer<T> comparer) : this(capacity, comparer, new ComparerZeroHashCodeEqualityComparer(comparer)) { }

        private IntervalHeap(int capacity, SCG.IComparer<T> comparer, SCG.IEqualityComparer<T> itemequalityComparer)
        {
            if (comparer == null)
                throw new NullReferenceException("Item comparer cannot be null");
            if (itemequalityComparer == null)
                throw new NullReferenceException("Item equality comparer cannot be null");

            this.comparer = comparer;
            this.itemequalityComparer = itemequalityComparer;

            Clear(capacity);
        }

        #endregion

        #region Members

        /// <summary>
        /// Clears all entries and resets the priority queue.
        /// </summary>
        public void Clear()
        {
            Clear(16);
        }

        /// <summary>
        /// Clears all entries and resets the priority queue.
        /// </summary>
        /// <param name="capacity">New initial capacity of the priority queue.</param>
        public void Clear(int capacity)
        {
            size = 0;

            int length = 1;
            while (length < capacity)
                length <<= 1;

            heap = new Interval[length];
        }

        /// <summary>
        /// Find the current least item of this priority queue.
        /// </summary>
        /// <returns>The least item.</returns>
        public T FindMin()
        {
            if (size == 0)
                throw new InvalidOperationException("Heap is empty");
            return heap[0].first;
        }

        /// <summary>
        /// Remove the least item from this  priority queue.
        /// </summary>
        /// <returns>The removed item.</returns>
        public T DeleteMin()
        {
            PriorityQueueHandle handle = null;
            return DeleteMin(out handle);
        }

        /// <summary>
        /// Find the current largest item of this priority queue.
        /// </summary>
        /// <returns>The largest item.</returns>
        public T FindMax()
        {
            if (size == 0)
                throw new InvalidOperationException("Heap is empty");
            else if (size == 1)
                return heap[0].first;
            else
                return heap[0].last;
        }

        /// <summary>
        /// Remove the largest item from this  priority queue.
        /// </summary>
        /// <returns>The removed item.</returns>
        public T DeleteMax()
        {
            PriorityQueueHandle handle = null;
            return DeleteMax(out handle);
        }

        /// <summary>
        /// The comparer object supplied at creation time for this collection
        /// </summary>
        /// <value>The comparer</value>
        public SCG.IComparer<T> Comparer { get { return comparer; } }

        /// <summary>
        /// Value is null since this collection has no equality concept for its items. 
        /// </summary>
        /// <value></value>
        public SCG.IEqualityComparer<T> EqualityComparer { get { return itemequalityComparer; } }

        /// <summary>
        /// Add an item to this priority queue.
        /// </summary>
        /// <param name="item">The item to add.</param>
        /// <returns>True</returns>
        public bool Add(T item)
        {
            if (add(null, item))
            {
                return true;
            }
            return false;
        }

        private bool add(PriorityQueueHandle itemhandle, T item)
        {
            if (size == 0)
            {
                size = 1;
                updateFirst(0, item, itemhandle);
                return true;
            }

            if (size == 2 * heap.Length)
            {
                Interval[] newheap = new Interval[2 * heap.Length];

                Array.Copy(heap, newheap, heap.Length);
                heap = newheap;
            }

            if (size % 2 == 0)
            {
                int i = size / 2, p = (i + 1) / 2 - 1;
                T tmp = heap[p].last;

                if (comparer.Compare(item, tmp) > 0)
                {
                    updateFirst(i, tmp, heap[p].lasthandle);
                    updateLast(p, item, itemhandle);
                    bubbleUpMax(p);
                }
                else
                {
                    updateFirst(i, item, itemhandle);

                    if (comparer.Compare(item, heap[p].first) < 0)
                        bubbleUpMin(i);
                }
            }
            else
            {
                int i = size / 2;
                T other = heap[i].first;

                if (comparer.Compare(item, other) < 0)
                {
                    updateLast(i, other, heap[i].firsthandle);
                    updateFirst(i, item, itemhandle);
                    bubbleUpMin(i);
                }
                else
                {
                    updateLast(i, item, itemhandle);
                    bubbleUpMax(i);
                }
            }
            size++;

            return true;
        }

        private void updateLast(int cell, T item, PriorityQueueHandle handle)
        {
            heap[cell].last = item;
            if (handle != null)
                handle.index = 2 * cell + 1;
            heap[cell].lasthandle = handle;
        }

        private void updateFirst(int cell, T item, PriorityQueueHandle handle)
        {
            heap[cell].first = item;
            if (handle != null)
                handle.index = 2 * cell;
            heap[cell].firsthandle = handle;
        }

        /// <summary>
        /// Add the elements from another collection with a more specialized item type 
        /// to this collection. 
        /// </summary>
        /// <typeparam name="U">The type of items to add</typeparam>
        /// <param name="items">The items to add</param>
        public void AddAll<U>(SCG.IEnumerable<U> items) where U : T
        {
            foreach (T item in items)
                add(null, item);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <value>True if this collection is empty.</value>
        public bool IsEmpty { get { return size == 0; } }

        /// <summary>
        /// 
        /// </summary>
        /// <value>The size of this collection</value>
        public int Count { get { return size; } }

        /// <summary>
        /// Choose some item of this collection. 
        /// </summary>
        /// <returns></returns>
        public T Choose()
        {
            if (size == 0)
                throw new InvalidOperationException("Heap is empty");
            return heap[0].first;
        }

        /// <summary>
        /// Get or set the item corresponding to a handle. 
        /// </summary>
        /// <param name="handle">The reference into the heap</param>
        /// <returns></returns>
        public T this[PriorityQueueHandle handle]
        {
            get
            {
                int cell;
                bool isfirst;
                checkHandle(handle, out cell, out isfirst);

                return isfirst ? heap[cell].first : heap[cell].last;
            }
            set
            {
                Replace(handle, value);
            }
        }

        /// <summary>
        /// Check safely if a handle is valid for this queue and if so, report the corresponding queue item.
        /// </summary>
        /// <param name="handle">The handle to check</param>
        /// <param name="item">If the handle is valid this will contain the corresponding item on output.</param>
        /// <returns>True if the handle is valid.</returns>
        public bool Find(PriorityQueueHandle handle, out T item)
        {
            PriorityQueueHandle myhandle = handle;
            if (myhandle == null)
            {
                item = default(T);
                return false;
            }
            int toremove = myhandle.index;
            int cell = toremove / 2;
            bool isfirst = toremove % 2 == 0;
            {
                if (toremove == -1 || toremove >= size)
                {
                    item = default(T);
                    return false;
                }
                PriorityQueueHandle actualhandle = isfirst ? heap[cell].firsthandle : heap[cell].lasthandle;
                if (actualhandle != myhandle)
                {
                    item = default(T);
                    return false;
                }
            }
            item = isfirst ? heap[cell].first : heap[cell].last;
            return true;
        }

        /// <summary>
        /// Add an item to the priority queue, receiving a 
        /// handle for the item in the queue, 
        /// or reusing an already existing handle.
        /// </summary>
        /// <param name="handle">On output: a handle for the added item. 
        /// On input: null for allocating a new handle, an invalid handle for reuse. 
        /// A handle for reuse must be compatible with this priority queue, 
        /// by being created by a priority queue of the same runtime type, but not 
        /// necessarily the same priority queue object.</param>
        /// <param name="item">The item to add.</param>
        /// <returns>True since item will always be added unless the call throws an exception.</returns>
        public bool Add(ref PriorityQueueHandle handle, T item)
        {
            PriorityQueueHandle myhandle = handle;
            if (myhandle == null)
                handle = myhandle = new PriorityQueueHandle();
            else
                if (myhandle.index != -1)
                    throw new ArgumentException("Handle not valid for reuse");
            if (add(myhandle, item))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Delete an item with a handle from a priority queue.
        /// </summary>
        /// <param name="handle">The handle for the item. The handle will be invalidated, but reusable.</param>
        /// <returns>The deleted item</returns>
        public T Delete(PriorityQueueHandle handle)
        {
            int cell;
            bool isfirst;
            PriorityQueueHandle myhandle = checkHandle(handle, out cell, out isfirst);

            T retval;
            myhandle.index = -1;
            int lastcell = (size - 1) / 2;

            if (cell == lastcell)
            {
                if (isfirst)
                {
                    retval = heap[cell].first;
                    if (size % 2 == 0)
                    {
                        updateFirst(cell, heap[cell].last, heap[cell].lasthandle);
                        heap[cell].last = default(T);
                        heap[cell].lasthandle = null;
                    }
                    else
                    {
                        heap[cell].first = default(T);
                        heap[cell].firsthandle = null;
                    }
                }
                else
                {
                    retval = heap[cell].last;
                    heap[cell].last = default(T);
                    heap[cell].lasthandle = null;
                }
                size--;
            }
            else if (isfirst)
            {
                retval = heap[cell].first;

                if (size % 2 == 0)
                {
                    updateFirst(cell, heap[lastcell].last, heap[lastcell].lasthandle);
                    heap[lastcell].last = default(T);
                    heap[lastcell].lasthandle = null;
                }
                else
                {
                    updateFirst(cell, heap[lastcell].first, heap[lastcell].firsthandle);
                    heap[lastcell].first = default(T);
                    heap[lastcell].firsthandle = null;
                }

                size--;
                if (heapifyMin(cell))
                    bubbleUpMax(cell);
                else
                    bubbleUpMin(cell);
            }
            else
            {
                retval = heap[cell].last;

                if (size % 2 == 0)
                {
                    updateLast(cell, heap[lastcell].last, heap[lastcell].lasthandle);
                    heap[lastcell].last = default(T);
                    heap[lastcell].lasthandle = null;
                }
                else
                {
                    updateLast(cell, heap[lastcell].first, heap[lastcell].firsthandle);
                    heap[lastcell].first = default(T);
                    heap[lastcell].firsthandle = null;
                }

                size--;
                if (heapifyMax(cell))
                    bubbleUpMin(cell);
                else
                    bubbleUpMax(cell);
            }

            return retval;
        }

        private PriorityQueueHandle checkHandle(PriorityQueueHandle handle, out int cell, out bool isfirst)
        {
            PriorityQueueHandle myhandle = handle;
            int toremove = myhandle.index;
            cell = toremove / 2;
            isfirst = toremove % 2 == 0;
            {
                if (toremove == -1 || toremove >= size)
                    throw new ArgumentException("Invalid handle, index out of range");
                PriorityQueueHandle actualhandle = isfirst ? heap[cell].firsthandle : heap[cell].lasthandle;
                if (actualhandle != myhandle)
                    throw new ArgumentException("Invalid handle, doesn't match queue");
            }
            return myhandle;
        }

        /// <summary>
        /// Replace an item with a handle in a priority queue with a new item. 
        /// Typically used for changing the priority of some queued object.
        /// </summary>
        /// <param name="handle">The handle for the old item</param>
        /// <param name="item">The new item</param>
        /// <returns>The old item</returns>
        public T Replace(PriorityQueueHandle handle, T item)
        {
            int cell;
            bool isfirst;
            checkHandle(handle, out cell, out isfirst);
            if (size == 0)
                throw new InvalidOperationException("Heap is empty");

            T retval;

            if (isfirst)
            {
                retval = heap[cell].first;
                heap[cell].first = item;
                if (size == 1)
                {
                }
                else if (size == 2 * cell + 1) // cell == lastcell
                {
                    int p = (cell + 1) / 2 - 1;
                    if (comparer.Compare(item, heap[p].last) > 0)
                    {
                        PriorityQueueHandle thehandle = heap[cell].firsthandle;
                        updateFirst(cell, heap[p].last, heap[p].lasthandle);
                        updateLast(p, item, thehandle);
                        bubbleUpMax(p);
                    }
                    else
                        bubbleUpMin(cell);
                }
                else if (heapifyMin(cell))
                    bubbleUpMax(cell);
                else
                    bubbleUpMin(cell);
            }
            else
            {
                retval = heap[cell].last;
                heap[cell].last = item;
                if (heapifyMax(cell))
                    bubbleUpMin(cell);
                else
                    bubbleUpMax(cell);
            }

            return retval;
        }

        /// <summary>
        /// Find the current least item of this priority queue.
        /// </summary>
        /// <param name="handle">On return: the handle of the item.</param>
        /// <returns>The least item.</returns>
        public T FindMin(out PriorityQueueHandle handle)
        {
            if (size == 0)
                throw new InvalidOperationException("Heap is empty");
            handle = heap[0].firsthandle;
            return heap[0].first;
        }

        /// <summary>
        /// Find the current largest item of this priority queue.
        /// </summary>
        /// <param name="handle">On return: the handle of the item.</param>
        /// <returns>The largest item.</returns>
        public T FindMax(out PriorityQueueHandle handle)
        {
            if (size == 0)
                throw new InvalidOperationException("Heap is empty");
            else if (size == 1)
            {
                handle = heap[0].firsthandle;
                return heap[0].first;
            }
            else
            {
                handle = heap[0].lasthandle;
                return heap[0].last;
            }
        }

        /// <summary>
        /// Remove the least item from this priority queue.
        /// </summary>
        /// <param name="handle">On return: the handle of the removed item.</param>
        /// <returns>The removed item.</returns>
        public T DeleteMin(out PriorityQueueHandle handle)
        {
            if (size == 0)
                throw new InvalidOperationException("Heap is empty");

            T retval = heap[0].first;
            PriorityQueueHandle myhandle = heap[0].firsthandle;
            handle = myhandle;
            if (myhandle != null)
                myhandle.index = -1;

            if (size == 1)
            {
                size = 0;
                heap[0].first = default(T);
                heap[0].firsthandle = null;
            }
            else
            {
                int lastcell = (size - 1) / 2;

                if (size % 2 == 0)
                {
                    updateFirst(0, heap[lastcell].last, heap[lastcell].lasthandle);
                    heap[lastcell].last = default(T);
                    heap[lastcell].lasthandle = null;
                }
                else
                {
                    updateFirst(0, heap[lastcell].first, heap[lastcell].firsthandle);
                    heap[lastcell].first = default(T);
                    heap[lastcell].firsthandle = null;
                }

                size--;
                heapifyMin(0);
            }

            return retval;

        }

        /// <summary>
        /// Remove the largest item from this priority queue.
        /// </summary>
        /// <param name="handle">On return: the handle of the removed item.</param>
        /// <returns>The removed item.</returns>
        public T DeleteMax(out PriorityQueueHandle handle)
        {
            if (size == 0)
                throw new InvalidOperationException("Heap is empty");

            T retval;
            PriorityQueueHandle myhandle;

            if (size == 1)
            {
                size = 0;
                retval = heap[0].first;
                myhandle = heap[0].firsthandle;
                if (myhandle != null)
                    myhandle.index = -1;
                heap[0].first = default(T);
                heap[0].firsthandle = null;
            }
            else
            {
                retval = heap[0].last;
                myhandle = heap[0].lasthandle;
                if (myhandle != null)
                    myhandle.index = -1;

                int lastcell = (size - 1) / 2;

                if (size % 2 == 0)
                {
                    updateLast(0, heap[lastcell].last, heap[lastcell].lasthandle);
                    heap[lastcell].last = default(T);
                    heap[lastcell].lasthandle = null;
                }
                else
                {
                    updateLast(0, heap[lastcell].first, heap[lastcell].firsthandle);
                    heap[lastcell].first = default(T);
                    heap[lastcell].firsthandle = null;
                }

                size--;
                heapifyMax(0);
            }

            handle = myhandle;
            return retval;
        }

        #endregion
    }
}
