﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Botcord.Core.Extensions
{
    public static class GenericsExtensions
    {
        public static int KiB(this int value) => value * 1024;
        public static int KB(this int value) => value * 1000;

        public static int MiB(this int value) => value.KiB() * 1024;
        public static int MB(this int value) => value.KB() * 1000;

        public static int GiB(this int value) => value.MiB() * 1024;
        public static int GB(this int value) => value.MB() * 1000;

        public static ulong KiB(this ulong value) => value * 1024;
        public static ulong KB(this ulong value) => value * 1000;

        public static ulong MiB(this ulong value) => value.KiB() * 1024;
        public static ulong MB(this ulong value) => value.KB() * 1000;

        public static ulong GiB(this ulong value) => value.MiB() * 1024;
        public static ulong GB(this ulong value) => value.MB() * 1000;

        public static int Second(this int value) => value * 1000;
        public static int Minute(this int value) => value.Second() * 60;
        public static int Hour(this int value) => value.Minute() * 60;
    }
}
