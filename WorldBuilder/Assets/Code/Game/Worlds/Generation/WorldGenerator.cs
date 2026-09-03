using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Game.Data;
using UnityEngine;

namespace Game.Worlds.Generation {
    public class WorldGenerator : IDisposable {
        private readonly BlockingCollection<IndexPos> _queue = new();
        private readonly ConcurrentDictionary<IndexPos, Action> _pendingRequests = new();
        private readonly Thread _workerThread;
        private readonly CancellationTokenSource _cts = new();

        private WorldManager Manager { get; }
        private WorldCache Cache => Manager.Cache;

        public WorldGenerator(WorldManager manager) {
            Manager = manager;

            _workerThread = new Thread(ProcessQueue) {
                IsBackground = true,
                Name = "WorldGeneratorThread"
            };
            _workerThread.Start();
        }

        /// <summary>
        /// Enfileira uma região para geração assíncrona. 
        /// Se a região já estiver pendente, apenas acumula o listener.
        /// </summary>
        public void EnqueueRegion(IndexPos regionIndex, Action onGenerated) {
            if (_queue.IsAddingCompleted) return;

            // Tenta adicionar ou acumular o callback no dicionário thread-safe
            _pendingRequests.AddOrUpdate(
                regionIndex,
                onGenerated, // Se for a primeira vez, cria a entrada com o callback
                (key, existingAction) => existingAction + onGenerated // Se já existir, acumula o listener
            );

            // Adiciona na fila de processamento apenas na primeira vez
            // (Verificamos se o callback inserido é igual ao recebido para saber se foi uma adição nova)
            if (_pendingRequests.TryGetValue(regionIndex, out var currentAction) && currentAction == onGenerated) {
                _queue.Add(regionIndex);
            }
        }

        private void ProcessQueue() {
            foreach (var regionIndex in _queue.GetConsumingEnumerable(_cts.Token)) {
                try {
                    // Remove do dicionário para pegar TODOS os listeners acumulados até este momento
                    _pendingRequests.TryRemove(regionIndex, out var accumulatedCallbacks);

                    // Processa a geração pesada
                    GenerateRegionInternal(regionIndex);

                    // Invoca todos os callbacks acumulados de uma só vez na worker thread
                    accumulatedCallbacks?.Invoke();
                } catch (OperationCanceledException) {
                    break;
                } catch (Exception ex) {
                    Debug.LogError($"Erro ao gerar região {regionIndex}: {ex}");
                }
            }
        }

        private void GenerateRegionInternal(IndexPos regionIndex) {
            Chunk[] chunks = new Chunk[Region.LodSize3[0]];
            int i = 0;
            int l = Region.LodSize1[0];
            for (int y = 0; y < l; y++)
            for (int z = 0; z < l; z++)
            for (int x = 0; x < l; x++) {
                var pos = regionIndex + (new IndexPos(x, y, z) * 32);
                var soil = GenerateSoil(pos);
                var chunk = new Chunk(pos, 0, soil) { CurrentVersion = 1 };
                chunks[i++] = chunk;
            }

            var allLods = BuildLods(regionIndex, chunks);
            Cache.PutRegion(regionIndex, allLods);
        }

        public void Dispose() {
            _queue.CompleteAdding();
            _cts.Cancel();
            _cts.Dispose();
            _queue.Dispose();
        }

        // --- Algoritmo de Geração e Lods ---

        public Chunk[][] BuildLods(IndexPos regionIndex, Chunk[] chunks) {
            Chunk[][] allChunks = new Chunk[Region.TotalLods][];
            allChunks[0] = chunks;
            
            int cLod = 0;
            while (cLod < Region.MaxLod) {
                int dim = Region.LodSize1[cLod];
                int nextLod = cLod + 1;
                
                allChunks[nextLod] = new Chunk[Region.LodSize3[nextLod]];
                for (int y = 0; y < dim; y += 2) 
                for (int z = 0; z < dim; z += 2) 
                for (int x = 0; x < dim; x += 2) {
                    var id = Region.GetLocalId(nextLod, x / 2 * (32 << nextLod), y / 2 * (32 << nextLod), z / 2 * (32 << nextLod));
                    var pos = Region.GetLocalPosition(nextLod, id);
                    allChunks[nextLod][id] = new Chunk(regionIndex + pos, nextLod, new [] {
                        chunks[LocalId(cLod, x + 0, y + 0, z + 0)], chunks[LocalId(cLod, x + 1, y + 0, z + 0)], 
                        chunks[LocalId(cLod, x + 0, y + 0, z + 1)], chunks[LocalId(cLod, x + 1, y + 0, z + 1)],
                        chunks[LocalId(cLod, x + 0, y + 1, z + 0)], chunks[LocalId(cLod, x + 1, y + 1, z + 0)], 
                        chunks[LocalId(cLod, x + 0, y + 1, z + 1)], chunks[LocalId(cLod, x + 1, y + 1, z + 1)]
                    });
                }

                chunks = allChunks[nextLod];
                cLod++; 
            }
            return allChunks;
        }

        private static int LocalId(int lod, int x, int y, int z) => Region.GetLocalId(lod, x, y, z);

        private ChunkSoil GenerateSoil(IndexPos pos) {
            var soil = new ChunkSoil();
            for (int x = 0; x < 32; x++) {
                for (int y = 0; y < 32; y++) {
                    for (int z = 0; z < 32; z++) {
                        var p = pos + new IndexPos(x, y, z);
                        /*if (p.y - 4 <= p.x && p.x > 0 && p.y > 0 && p.z > 0) {
                            soil.SetDensity(x, y, z, 1);
                            soil.SetMaterial(x, y, z, 1);
                        } else {
                            soil.SetDensity(x, y, z, 0);
                            soil.SetMaterial(x, y, z, 0);
                        }*/
                        if (x >= 8 && x < 24 && y >= 8 && y < 24 && z >= 8 && z < 24) {
                            soil.SetDensity(x, y, z, 1);
                            soil.SetMaterial(x, y, z, 1);
                        } else {
                            soil.SetDensity(x, y, z, 0);
                            soil.SetMaterial(x, y, z, 0);
                        }
                    }
                }
            }
            return soil;
        }
    }
}