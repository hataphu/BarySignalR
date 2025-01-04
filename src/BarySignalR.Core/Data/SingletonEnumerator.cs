using System.Collections;

namespace BarySignalR.Core.Data
{
    [GenerateSerializer]
    public class SingletonEnumerator<T> : IEnumerator<T>
    {
        private bool done = false;
        public T Current { get; }

        public SingletonEnumerator(T value)
        {
            this.Current = value;
        }

        object IEnumerator.Current => Current!;

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            return !done && (done = true);
        }

        public void Reset()
        {
            done = false;
        }
    }
}
