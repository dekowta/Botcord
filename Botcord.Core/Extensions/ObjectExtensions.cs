using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace Botcord.Core.Extensions
{
    public static class ObjectExtensions
    {
        public static bool TryCast<T>(this object e, out T casted)
        {
            try
            {
                if (e is T)
                {
                    casted = (T)e;
                    return true;
                }

                casted = default(T);
                return false;
            }
            catch
            {
                casted = default(T);
                return false;
            }
        }

        public static string AssemblyLocation(this object e)
        {
            try
            {
                return e.GetType().GetTypeInfo().Assembly.Location;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
