using System;
using System.Collections.Generic;

namespace Portals {
    public class ObjectPool<T> {
        public int Count { get; private set; }

        private Queue<T> _available;
        private Func<T> _createObjectFunc;

        public ObjectPool(int numPreallocated = 0, Func<T> createObjectFunc = null) {
            _available = new Queue<T>();
            _createObjectFunc = createObjectFunc;

            if (numPreallocated > 0) {
                this.Give(MakeObject());
            }
        }

        public T Take() {
            T t = default(T);
            if (_available.Count > 0) {
                t = _available.Dequeue();
                Count--;
            } else {
                t = MakeObject();
            }
            return t;
        }

        public void Give(T t) {
            _available.Enqueue(t);
            Count++;
        }

        private T MakeObject() {
            if (_createObjectFunc != null) {
                return _createObjectFunc();
            } else {
                throw new System.InvalidOperationException("Cannot automatically create new objects without a factory method");
            }
        }
    }
}
