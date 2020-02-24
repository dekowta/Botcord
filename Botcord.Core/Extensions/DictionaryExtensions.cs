using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using ObjectDictionary = System.Collections.Generic.Dictionary<string, object>;

namespace Botcord.Core.Extensions
{
    public static class DictionaryExtensions
    {
        public static object GetValue(this ObjectDictionary dic, string key)
        {
            if (dic.ContainsKey(key))
                return dic[key];
            else
                return null;
        }

        public static T GetValue<T>(this ObjectDictionary dic, string key)
        {
            if (dic.ContainsKey(key) && dic[key] is T)
                return (T)dic[key];
            else
                return default(T);
        }

        public static List<string> GetKeys(this ObjectDictionary dic)
        {
            return dic.Keys.Select(x => x.ToString()).ToList();
        }
    }
}
