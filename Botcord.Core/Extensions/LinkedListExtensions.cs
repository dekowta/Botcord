using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Botcord.Core.Extensions
{
    public static class LinkedListExtensions
    {
        public static void Shuffle<T>(this LinkedList<T> list)
        {
            Random rand = new Random();

            for (LinkedListNode<T> n = list.First; n != null; n = n.Next)
            {
                T v = n.Value;
                if (rand.Next(0, 2) == 1)
                {
                    n.Value = list.Last.Value;
                    list.Last.Value = v;
                }
                else
                {
                    n.Value = list.First.Value;
                    list.First.Value = v;
                }
            }
        }

        public static int IndexOf<T>(this LinkedList<T> list, T item)
        {
            int i = list.TakeWhile(n => !n.Equals(item)).Count();
            if (i == list.Count)
                return -1;
            else
                return i;
        }
    }
}
