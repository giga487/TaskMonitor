using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskMonitor.Utility
{
    public class LimitedQueue<T>
    {
        private readonly int maxSize;
        private readonly ConcurrentQueue<T> queue;
        public int Count
        {
            get
            {
                try
                {
                    return queue.Count;
                }
                catch (Exception ex)
                {
                    return 0;
                }
            }
        }
        public LimitedQueue(int maxSize)
        {
            this.maxSize = maxSize;
            this.queue = new ConcurrentQueue<T>();
        }

        public void Add(T item)
        {
            if (queue.Count >= maxSize)
            {
                queue.TryDequeue(out var _); // Rimuove l'elemento più vecchio
            }

            queue.Enqueue(item);
        }

        public double GetSumOrList()
        {
            if (typeof(T).IsNumericType())
            {
                try
                {
                    return queue.Sum(item => Convert.ToDouble(item));
                }
                catch
                {
                    return 0;
                }
            }

            return 0;
        }

        public IEnumerable<T> GetItems()
        {
            return queue;
        }
    }
    public static class TypeExtensions
    {
        public static bool IsNumericType(this Type type)
        {
            if (type == null)
            {
                return false;
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }
    }
}
