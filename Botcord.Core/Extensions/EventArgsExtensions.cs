using System;

namespace Botcord.Core.Extensions
{
    public static class EventArgsExtensions
    {
        public static bool TryCast<T>(this EventArgs e, out T casted)
        {
            try
            {
                casted = (T)Convert.ChangeType(e, typeof(T));
                return true;
            }
            catch
            {
                casted = default(T);
                return false;
            }
        }

    }
}
