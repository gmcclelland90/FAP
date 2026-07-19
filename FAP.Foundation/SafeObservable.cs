#region Copyright Kayomani 2010.  Licensed under the GPLv3 (Or later version), Expand for details. Do not remove this notice.
/**
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or any 
    later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * */
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Specialized;
using System.Threading;
using System.Collections;
using Fap.Foundation.Threading;

namespace Fap.Foundation
{
    public class SafeObservableStatic
    {
        public static IUiDispatcher? UiDispatcher { get; set; }
    }

    public class SafeObservable<T> : IList<T>, INotifyCollectionChanged
    {
        private IList<T> collection = new List<T>();
        private readonly IUiDispatcher? dispatcher;
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        private ReaderWriterLock sync = new ReaderWriterLock();

        public SafeObservable()
        {
            dispatcher = SafeObservableStatic.UiDispatcher;
        }

        private void RunOnUi(Action action)
        {
            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }

        private TResult RunOnUi<TResult>(Func<TResult> func)
        {
            if (dispatcher == null || dispatcher.CheckAccess())
                return func();

            TResult result = default!;
            dispatcher.Invoke(() => result = func());
            return result;
        }

        public void Add(T item)
        {
            RunOnUi(() => DoAdd(item));
        }

        public void AddRotate(T item, int max)
        {
            RunOnUi(() => DoAddRotate(item, max));
        }

        private void DoAddRotate(T item, int max)
        {
            T removed = default(T);
            sync.AcquireWriterLock(Timeout.Infinite);
            collection.Add(item);
            if (collection.Count > max && max > 0)
            {
                removed = collection[0];
                collection.RemoveAt(0);
            }
            sync.ReleaseWriterLock();
            if (CollectionChanged != null && null != removed)
                CollectionChanged(this,
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, 0));
            if (CollectionChanged != null)
                CollectionChanged(this,
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
        }

        public bool AddUnique(T item)
        {
            return RunOnUi(() => doAddUnique(item));
        }

        private bool doAddUnique(T item)
        {
            bool added = false;
            sync.AcquireWriterLock(Timeout.Infinite);
            if (!collection.Contains(item))
            {
                collection.Add(item);
                added = true;
            }
            sync.ReleaseWriterLock();
            if (added && CollectionChanged != null)
                CollectionChanged(this,
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
            return added;
        }

        public void Lock()
        {
            sync.AcquireWriterLock(Timeout.Infinite);
        }

        public void Unlock()
        {
            sync.ReleaseWriterLock();
        }

        public List<T> ToList()
        {
            sync.AcquireReaderLock(Timeout.Infinite);
            var list = collection.ToList();
            sync.ReleaseReaderLock();
            return list;
        }

        private void DoAdd(T item)
        {
            sync.AcquireWriterLock(Timeout.Infinite);
            collection.Add(item);
            sync.ReleaseWriterLock();
            if (CollectionChanged != null)
                CollectionChanged(this,
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
        }

        public void Clear()
        {
            RunOnUi(DoClear);
        }

        private void DoClear()
        {
            sync.AcquireWriterLock(Timeout.Infinite);
            collection.Clear();
            sync.ReleaseWriterLock();
            if (CollectionChanged != null)
                CollectionChanged(this,
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool Contains(T item)
        {
            sync.AcquireReaderLock(Timeout.Infinite);
            var result = collection.Contains(item);
            sync.ReleaseReaderLock();
            return result;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            sync.AcquireReaderLock(Timeout.Infinite);
            collection.CopyTo(array, arrayIndex);
            sync.ReleaseReaderLock();
        }

        public int Count
        {
            get
            {
                sync.AcquireReaderLock(Timeout.Infinite);
                var result = collection.Count;
                sync.ReleaseReaderLock();
                return result;
            }
        }

        public bool IsReadOnly
        {
            get { return collection.IsReadOnly; }
        }

        public bool Remove(T item)
        {
            return RunOnUi(() => DoRemove(item));
        }

        public bool Remove(IList items)
        {
            return RunOnUi(() =>
            {
                sync.AcquireWriterLock(Timeout.Infinite);
                foreach (var item in items)
                {
                    if (item is T t && collection.Contains(t))
                        collection.Remove(t);
                }
                sync.ReleaseWriterLock();
                CollectionChanged?.Invoke(this, new
                    NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                return true;
            });
        }

        private bool DoRemove(T item)
        {
            sync.AcquireWriterLock(Timeout.Infinite);
            var index = collection.IndexOf(item);
            if (index == -1)
            {
                sync.ReleaseWriterLock();
                return false;
            }
            var result = collection.Remove(item);
            sync.ReleaseWriterLock();
            if (result && CollectionChanged != null)
                CollectionChanged(this, new
                    NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
            return result;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return collection.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return collection.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            sync.AcquireReaderLock(Timeout.Infinite);
            var result = collection.IndexOf(item);
            sync.ReleaseReaderLock();
            return result;
        }

        public void Insert(int index, T item)
        {
            RunOnUi(() => DoInsert(index, item));
        }

        private void DoInsert(int index, T item)
        {
            sync.AcquireWriterLock(Timeout.Infinite);
            collection.Insert(index, item);
            sync.ReleaseWriterLock();
            if (CollectionChanged != null)
                CollectionChanged(this,
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        public void RemoveAt(int index)
        {
            RunOnUi(() => DoRemoveAt(index));
        }

        private void DoRemoveAt(int index)
        {
            sync.AcquireWriterLock(Timeout.Infinite);
            if (collection.Count == 0 || collection.Count <= index)
            {
                sync.ReleaseWriterLock();
                return;
            }
            T item = collection[index];
            collection.RemoveAt(index);
            sync.ReleaseWriterLock();
            if (CollectionChanged != null)
                CollectionChanged(this,
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
        }

        public T this[int index]
        {
            get
            {
                sync.AcquireReaderLock(Timeout.Infinite);
                var result = collection[index];
                sync.ReleaseReaderLock();
                return result;
            }
            set
            {
                sync.AcquireWriterLock(Timeout.Infinite);
                if (collection.Count == 0 || collection.Count <= index)
                {
                    sync.ReleaseWriterLock();
                    return;
                }
                collection[index] = value;
                sync.ReleaseWriterLock();
            }
        }
    }
}
