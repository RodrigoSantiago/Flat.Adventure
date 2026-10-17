using System;
using System.Collections.Generic;

namespace Game.Data.Queues {
    public class ProgressToken {
        private float localCapacity;
        private float localProgress;
        private List<(ProgressToken child, float weight)> children;

        public ProgressToken() {
            localCapacity = 1f;
            localProgress = 0f;
        }

        private ProgressToken(float capacity) {
            localCapacity = capacity;
            localProgress = 0f;
        }

        public float Progress {
            get {
                float total = localProgress;

                if (children != null) {
                    foreach (var (child, weight) in children) {
                        total += child.Progress * weight;
                    }
                }

                return MathF.Min(1f, total);
            }
        }

        public ProgressToken Derive(float amount) {
            amount = MathF.Max(0, amount);

            float remainingCapacity = localCapacity - localProgress;

            float actualWeight = MathF.Min(remainingCapacity, amount);

            localCapacity -= actualWeight;

            var child = new ProgressToken();

            children ??= new List<(ProgressToken, float)>();
            children.Add((child, actualWeight));

            return child;
        }

        public void Advance(float step) {
            if (step <= 0f) return;
            
            localProgress = MathF.Min(localCapacity, localProgress + step);
        }
    }
}