﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLREdit
{
    internal static class LoggingSystem
    {
        public static void LogInfo(string info)
        {
#if DEBUG
            var now = DateTime.Now;
            Trace.WriteLine("[" + now + "]Info:" + info);
#endif
        }

        public static void LogWarning(string info)
        {
#if DEBUG
            var now = DateTime.Now;
            Trace.WriteLine("[" + now + "]Warning:" + info);
#endif
        }

        public static void LogError(string info)
        {
#if DEBUG
            var now = DateTime.Now;
            Trace.WriteLine("[" + now + "]Error:" + info);
#endif
        }
    }
}