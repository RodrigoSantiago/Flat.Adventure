using System;
using System.Threading;

namespace Game.Utils {
    public static class UtilsParallel {
        private static readonly int WorkerCount = Math.Max(1, Environment.ProcessorCount - 1);
        private static readonly WorkerThread[] Workers;

        private static readonly object SyncLock = new object();
        private static readonly ManualResetEventSlim CompletedEvent = new ManualResetEventSlim(false);

        private static Action<int> CurrentAction;
        private static int RemainingWorkers;
        private static Exception CapturedException;

        static UtilsParallel() {
            Workers = new WorkerThread[WorkerCount];
            for (int i = 0; i < WorkerCount; i++) {
                Workers[i] = new WorkerThread();
            }
        }
        
        public static void For(int fromInclusive, int toExclusive, Action<int> action) {
            int count = toExclusive - fromInclusive;
            if (count <= 0) return;
            if (action == null) throw new ArgumentNullException(nameof(action));

            lock (SyncLock) {
                int activeWorkers = Math.Min(WorkerCount, count);
                
                CurrentAction = action;
                RemainingWorkers = activeWorkers;
                CapturedException = null;
                CompletedEvent.Reset();

                int baseChunk = count / activeWorkers;
                int remainder = count % activeWorkers;
                int currentStart = fromInclusive;

                for (int i = 0; i < activeWorkers; i++) {
                    int chunkSize = baseChunk + (i < remainder ? 1 : 0);
                    Workers[i].SetRange(currentStart, currentStart + chunkSize);
                    currentStart += chunkSize;
                }

                for (int i = 0; i < activeWorkers; i++) {
                    Workers[i].Wake();
                }

                CompletedEvent.Wait();

                CurrentAction = null;

                if (CapturedException != null) {
                    var ex = CapturedException;
                    CapturedException = null;
                    throw new AggregateException("UtilsParallel inner exception.", ex);
                }
            }
        }

        private static void OnWorkerFinished() {
            if (Interlocked.Decrement(ref RemainingWorkers) == 0) {
                CompletedEvent.Set();
            }
        }

        private sealed class WorkerThread {
            private readonly AutoResetEvent wakeSignal = new AutoResetEvent(false);
            private int fromIndex;
            private int toIndex;

            public WorkerThread() {
                var thread = new Thread(Run) {
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal,
                    Name = "UtilsParallel-Worker"
                };
                thread.Start();
            }

            public void SetRange(int from, int to) {
                fromIndex = from;
                toIndex = to;
            }

            public void Wake() {
                wakeSignal.Set();
            }

            private void Run() {
                while (true) {
                    wakeSignal.WaitOne();

                    Action<int> action = CurrentAction;
                    int start = fromIndex;
                    int end = toIndex;

                    try {
                        for (int i = start; i < end; i++) {
                            action(i);
                        }
                    } catch (Exception ex) {
                        Interlocked.CompareExchange(ref CapturedException, ex, null);
                    } finally {
                        OnWorkerFinished();
                    }
                }
            }
        }
    }
}