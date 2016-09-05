using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFD_COMMON.Utils.Extensions
{
    public static class CollectionExtensions
    {
        public static IEnumerable<IEnumerable<T>> SplitInChunks<T>(this IEnumerable<T> enumerable, int chunkSize)
        {
            if (chunkSize <= 0)
            {
                throw new ArgumentException("chunkSize must be greater than 0.");
            }

            List<IEnumerable<T>> retVal = new List<IEnumerable<T>>();

            List<T> list = enumerable.ToList();
            int index = 0;
            while (index < list.Count)
            {
                int count = list.Count - index > chunkSize ? chunkSize : list.Count - index;
                retVal.Add(list.GetRange(index, count));

                index += chunkSize;
            }
            return retVal;
        }
    }
}
