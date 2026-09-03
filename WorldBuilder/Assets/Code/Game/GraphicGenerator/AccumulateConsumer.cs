using System.Collections.Generic;
using UnityEngine;

namespace Game.GraphicGenerator {
    public class AccumulateConsumer<T> where T : IAccumulateTask<T> {

        private readonly int maxTaskGroup;
        private readonly List<T> taskList = new();
        private readonly List<T> bestTasks = new();
        private readonly HashSet<T> toRemoveSet = new();

        public AccumulateConsumer(int maxTaskGroup) {
            this.maxTaskGroup = maxTaskGroup;
        }

        public void Accumulate(T task) {
            for (int i = 0; i < taskList.Count; i++) {
                if (taskList[i].IsReplaceable(task)) {
                    taskList[i] = task;
                    return;
                }
            }
            taskList.Add(task);
        }

        public T Consume() {
            int count = taskList.Count;
            if (count == 0) return default;
    
            var first = taskList[0];
            if (count == 1) {
                taskList.RemoveAt(0);
                return first;
            }

            bestTasks.Clear();
            int capacity = maxTaskGroup - first.Count;

            for (int i = 1; i < count; i++) {
                var candidate = taskList[i];
                int sim = candidate.Similarity(first);

                if (bestTasks.Count < capacity) {
                    bestTasks.Add(candidate);
                } else {
                    int minSimIndex = 0;
                    int minSimValue = bestTasks[0].Similarity(first);

                    for (int j = 1; j < bestTasks.Count; j++) {
                        int currentSim = bestTasks[j].Similarity(first);
                        if (currentSim < minSimValue) {
                            minSimValue = currentSim;
                            minSimIndex = j;
                        }
                    }

                    if (sim > minSimValue) {
                        bestTasks[minSimIndex] = candidate;
                    }
                }
            }

            toRemoveSet.Clear();
            toRemoveSet.Add(first);

            foreach (var task in bestTasks) {
                first.Group(task);
                toRemoveSet.Add(task);
            }
            taskList.RemoveAll(toRemoveSet.Contains);

            return first;
        }
    }
}