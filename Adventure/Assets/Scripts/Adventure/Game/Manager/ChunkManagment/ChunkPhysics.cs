using System;
using System.Collections.Generic;
using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Game.Manager.ChunkManagment {
    public class ChunkPhysics {

        private static readonly float limit = -0.001f;
        private static readonly float normalShift = 0.0025f;
        private static readonly float normalUshift = 1 - 0.0025f;
        private static Vector3 notFound = new Vector3(-1, -1, -1);
        private static readonly Vector3Int[] VertexOffset ={
            new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(1, 1, 0), new Vector3Int(0, 1, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1), new Vector3Int(1, 1, 1), new Vector3Int(0, 1, 1)
        };
        
        private readonly ChunkManager manager;
        private readonly Dictionary<Vector3Int, ChunkHolder> chunks;
        
        private CellData[] readVoxels = new CellData[8];
        private Vector3Int voxelLastChunkPos = new Vector3Int(-1, -1, -1);
        private ChunkHolder voxelLastHolder;
        private Vector3Int voxelLastCell;

        public ChunkPhysics(ChunkManager manager, Dictionary<Vector3Int, ChunkHolder> chunks) {
            this.manager = manager;
            this.chunks = chunks;
        }

        public ChunkCollisionHit Raycast(Vector3 start, Vector3 dir, float maxDistance) {
            bool cast = Raycast(start, dir, maxDistance, out var dis, out var col, out var nor, out var vox);
            return new ChunkCollisionHit(cast, start, dir, dis, col, nor, vox);
        }
        
        public bool Raycast(Vector3 start, Vector3 dir, float maxDistance, out float distance, out Vector3 collision, out Vector3 normal, out Voxel voxel) {
            if (Raycast(start, dir, maxDistance, out distance, out collision, out normal)) {
                int found = 0;
                float dist = 0;
                for (int i = 0; i < 8; i++) {
                    Vector3Int pos = readVoxels[i].cell;
                    float d = (voxelLastCell.x - pos.x) * (voxelLastCell.x - pos.x) +
                              (voxelLastCell.y - pos.y) * (voxelLastCell.y - pos.y) +
                              (voxelLastCell.z - pos.z) * (voxelLastCell.z - pos.z);
                    if (i == 0 || d < dist) {
                        dist = d;
                        found = i;
                    }
                }

                voxel = readVoxels[found].voxel;
                return true;
            }

            voxel = new Voxel(0f, 0);
            return false;
        }

        public bool Raycast(Vector3 start, Vector3 dir, float maxDistance, out float distance, out Vector3 collision, out Vector3 normal) {
            if (Raycast(start, dir, maxDistance, out distance, out collision)) {
                normal = GetNormalAt(collision);
                return true;
            }

            normal = dir;
            return false;
        }

        public bool Raycast(Vector3 start, Vector3 dir, float maxDistance, out float distance, out Vector3 collision) {
            if (Raycast(start, dir, maxDistance, out distance)) {
                collision = start + dir * distance;
                return true;
            }

            collision = start + dir * maxDistance;
            return false;
        }

        public bool Raycast(Vector3 start, Vector3 dir, float maxDistance, out float distance) {
            if (Mathf.Max(Math.Abs(dir.x), Math.Abs(dir.y), Math.Abs(dir.z)) <= 0.001) {
                distance = maxDistance;
                return false;
            }
            
            Vector3Int gridPos = new Vector3Int((int)start.x, (int)start.y, (int)start.z);
            Vector3Int rayStep = new Vector3Int(dir.x < 0 ? -1 : 1, dir.y < 0 ? -1 : 1, dir.z < 0 ? -1 : 1);
            Vector3 rayStepSize = new Vector3(Math.Abs(1f / dir.x), Math.Abs(1f / dir.y), Math.Abs(1f / dir.z));
            Vector3 rayLen = new Vector3(
                dir.x < 0 ? (start.x - (gridPos.x)) * rayStepSize.x : ((gridPos.x + 1) - start.x) * rayStepSize.x,
                dir.y < 0 ? (start.y - (gridPos.y)) * rayStepSize.y : ((gridPos.y + 1) - start.y) * rayStepSize.y,
                dir.z < 0 ? (start.z - (gridPos.z)) * rayStepSize.z : ((gridPos.z + 1) - start.z) * rayStepSize.z
            );
            Vector3 pos = start;
            
            float pDistance = 0.0f;
            float fDistance = 0.0f;
            while (fDistance < maxDistance) {
                if (rayLen.x <= rayLen.y && rayLen.x <= rayLen.z) {
                    gridPos.x += rayStep.x;
                    fDistance = rayLen.x;
                    rayLen.x += rayStepSize.x;
                } else if (rayLen.y <= rayLen.z) {
                    gridPos.y += rayStep.y;
                    fDistance = rayLen.y;
                    rayLen.y += rayStepSize.y;
                } else {
                    gridPos.z += rayStep.z;
                    fDistance = rayLen.z;
                    rayLen.z += rayStepSize.z;
                }

                if (fDistance > maxDistance) {
                    fDistance = maxDistance;
                }

                float tD = 0;
                Vector3 next = start + dir * fDistance;
                Vector3 col = GetCollision(pos, next, dir, 1, ref tD);
                if (col.x > -1) {
                    distance = pDistance + tD;
                    return true;
                }
                pos = next;
                pDistance = fDistance;
            }

            distance = maxDistance;
            return false;
        }
        
        private float ReadValueAt(Vector3 pos) {
            ReadCell(pos);
            
            float mx = pos.x - voxelLastCell.x;
            float my = pos.y - voxelLastCell.y;
            float mz = pos.z - voxelLastCell.z;

            float ix1 = Mathf.Lerp(readVoxels[0].voxel.volume, readVoxels[1].voxel.volume, mx);
            float ix2 = Mathf.Lerp(readVoxels[3].voxel.volume, readVoxels[2].voxel.volume, mx);
            float iy1 = Mathf.Lerp(ix1, ix2, my);
	    
            float ix3 = Mathf.Lerp(readVoxels[4].voxel.volume, readVoxels[5].voxel.volume, mx);
            float ix4 = Mathf.Lerp(readVoxels[7].voxel.volume, readVoxels[6].voxel.volume, mx);
            float iy2 = Mathf.Lerp(ix3, ix4, my);

            return Mathf.Lerp(iy1, iy2, mz) - 0.5f;
        }
        
        private float GetValueAt(Vector3 pos) {
            float mx = pos.x - voxelLastCell.x;
            float my = pos.y - voxelLastCell.y;
            float mz = pos.z - voxelLastCell.z;

            float ix1 = Mathf.Lerp(readVoxels[0].voxel.volume, readVoxels[1].voxel.volume, mx);
            float ix2 = Mathf.Lerp(readVoxels[3].voxel.volume, readVoxels[2].voxel.volume, mx);
            float iy1 = Mathf.Lerp(ix1, ix2, my);
	    
            float ix3 = Mathf.Lerp(readVoxels[4].voxel.volume, readVoxels[5].voxel.volume, mx);
            float ix4 = Mathf.Lerp(readVoxels[7].voxel.volume, readVoxels[6].voxel.volume, mx);
            float iy2 = Mathf.Lerp(ix3, ix4, my);

            return Mathf.Lerp(iy1, iy2, mz) - 0.5f;
        }

        private Vector3 GetNormalAt(Vector3 pos) {
            float mx = pos.x - voxelLastCell.x;
            float my = pos.y - voxelLastCell.y;
            float mz = pos.z - voxelLastCell.z;

            float v_nx = 0, v_px = 0, v_ny = 0, v_py = 0, v_nz = 0, v_pz = 0;
            if (mx >= normalShift) v_nx = GetValueAt(new Vector3(pos.x - normalShift, pos.y, pos.z));
            if (mx <= normalUshift) v_px = GetValueAt(new Vector3(pos.x + normalShift, pos.y, pos.z));
            if (my >= normalShift) v_ny = GetValueAt(new Vector3(pos.x, pos.y - normalShift, pos.z));
            if (my <= normalUshift) v_py = GetValueAt(new Vector3(pos.x, pos.y + normalShift, pos.z));
            if (mz >= normalShift) v_nz = GetValueAt(new Vector3(pos.x, pos.y, pos.z - normalShift));
            if (mz <= normalUshift) v_pz = GetValueAt(new Vector3(pos.x, pos.y, pos.z + normalShift));

            if (mx < normalShift) v_nx = ReadValueAt(new Vector3(pos.x - normalShift, pos.y, pos.z));
            if (mx > normalUshift) v_px = ReadValueAt(new Vector3(pos.x + normalShift, pos.y, pos.z));
            if (my < normalShift) v_ny = ReadValueAt(new Vector3(pos.x, pos.y - normalShift, pos.z));
            if (my > normalUshift) v_py = ReadValueAt(new Vector3(pos.x, pos.y + normalShift, pos.z));
            if (mz < normalShift) v_nz = ReadValueAt(new Vector3(pos.x, pos.y, pos.z - normalShift));
            if (mz > normalUshift) v_pz = ReadValueAt(new Vector3(pos.x, pos.y, pos.z + normalShift));
            
            var normal = -new Vector3(v_px - v_nx, v_py - v_ny, v_pz - v_nz).normalized;
            if (float.IsNaN(normal.x) || float.IsNaN(normal.y) || float.IsNaN(normal.z)) {
                return new Vector3(0, 1, 0);
            }

            return normal;
        }

        private void ReadCell(Vector3 cell) {
            Vector3Int iCell = new Vector3Int(Mathf.FloorToInt(cell.x), Mathf.FloorToInt(cell.y), Mathf.FloorToInt(cell.z));
            if (iCell == voxelLastCell) return;

            voxelLastCell = iCell;
            for (int i = 0; i < 8; i++) {
                Vector3Int pos = manager.settings.Pos(voxelLastCell + VertexOffset[i]);
                Vector3Int chunkPos = new Vector3Int(pos.x / 16 * 16, pos.y / 16 * 16, pos.z / 16 * 16);
                if (chunkPos != voxelLastChunkPos) {
                    voxelLastChunkPos = chunkPos;
                    voxelLastHolder = null;
                    chunks.TryGetValue(chunkPos, out voxelLastHolder);
                }

                readVoxels[i].cell = pos;
                readVoxels[i].voxel = voxelLastHolder?.chunk == null ? new Voxel() : voxelLastHolder.chunk[pos - chunkPos];
            }
        }
        
        private Vector3 GetCollision(Vector3 posA, Vector3 posB, Vector3 dir, float sign, ref float dist) {
            ReadCell(Vector3.Lerp(posA, posB, 0.5f));
            
            bool test = false;
            for (int i = 0; i < 8; i++) {
                if (readVoxels[i].voxel.volume > 0.5) {
                    test = true;
                    break;
                }
            }

            if (!test) {
                // No Collision
                return notFound;
            }
            
            float vA = GetValueAt(posA);
            if (vA > 0) {
                // Collision on A
                return posA;
            }
            
            float vB = GetValueAt(posB);
            Vector3 posC = posB;
            
            if (vB * sign < limit * sign) {
                posC = Vector3.Lerp(posA, posB, 0.5f);
                float vC = GetValueAt(posC);
                if (vC * sign < limit * sign) {
                    // No Collision
                    return notFound;
                }
            }

            posA += dir * -vA; // sign?
            while (Mathf.FloorToInt(posA.x) == voxelLastCell.x && 
                   Mathf.FloorToInt(posA.y) == voxelLastCell.y &&
                   Mathf.FloorToInt(posA.z) == voxelLastCell.z) {
                dist -= vA;
                vA = GetValueAt(posA);
                if (vA * sign >= limit * sign) {
                    // Ray Collision
                    return posA;
                }
                posA += dir * -vA; // sign?
            }

            // Collision on B or C
            return posC;
        }
        
        private struct CellData {
            public Voxel voxel;
            public Vector3Int cell;

            public CellData(Voxel voxel, Vector3Int cell) {
                this.voxel = voxel;
                this.cell = cell;
            }
        }
    }
}