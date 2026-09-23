using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.DebugUtils {
    public class GameDebug {
        private static Dictionary<string, int> counts = new();
        private static Dictionary<string, TimeSpan> sums = new();
        
        public static void AddMetric(string name, TimeSpan span) {
            /*if (!counts.ContainsKey(name)) {
                counts[name] = 0;
                sums[name] = TimeSpan.Zero;
            }
            counts[name] += 1;
            sums[name] += span;*/
        }
        
        public static void LodMetrics() {
            /*var keys = counts.Keys.OrderBy(s => s);
            foreach (var key in keys) {
                Debug.Log(key.PadLeft(10, ' ') + ":" + (sums[key] / counts[key]));
                counts[key] = 0;
                sums[key] = TimeSpan.Zero;
            }*/
        }
    }
}