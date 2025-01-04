using System.Collections;

namespace BarySignalR.Core.Data
{
    public class EmptyEnumerator<T> : IEnumerator<T>
    {
        private EmptyEnumerator()
        {
        }
        public readonly static EmptyEnumerator<T> Instance = new EmptyEnumerator<T>();
        public T Current => default!;

        object IEnumerator.Current => default!;

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            return false;
        }

        public void Reset()
        {
        }
    }
}
