using System;
using System.Collections.Generic;
using System.Text;

namespace Botcord.Core.Extensions
{
    public static class DateTimeExtensions
    {
        public static double Epoch(this DateTime dt) => dt.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
    }
}
