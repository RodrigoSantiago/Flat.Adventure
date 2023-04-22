using System;
using System.Collections.Generic;
using UnityEngine;

public class TableGenerator {
    
	private static readonly int[,] EdgeConnection = new int[,] {
		{0, 1}, {1, 2}, {2, 3}, {3, 0},
		{4, 5}, {5, 6}, {6, 7}, {7, 4},
		{0, 4}, {1, 5}, {2, 6}, {3, 7}
	};

	public int rotX(int number) {
		for (int j = 0; j < 8; j++) {
			caseVertexTemp[j] = (number & (1 << j)) == (1 << j);
		}

		caseVertex[0] = caseVertexTemp[3];
		caseVertex[1] = caseVertexTemp[0];
		caseVertex[2] = caseVertexTemp[1];
		caseVertex[3] = caseVertexTemp[2];

		caseVertex[4] = caseVertexTemp[7];
		caseVertex[5] = caseVertexTemp[4];
		caseVertex[6] = caseVertexTemp[5];
		caseVertex[7] = caseVertexTemp[6];

		int flagIndex = 0;
		for (int i = 0; i < 8; i++) {
			if (caseVertex[i]) flagIndex |= 1 << i;
		}

		return flagIndex;
	}

	public int rotY(int number) {
		for (int j = 0; j < 8; j++) {
			caseVertexTemp[j] = (number & (1 << j)) == (1 << j);
		}

		caseVertex[3] = caseVertexTemp[0];
		caseVertex[7] = caseVertexTemp[3];
		caseVertex[4] = caseVertexTemp[7];
		caseVertex[0] = caseVertexTemp[4];

		caseVertex[2] = caseVertexTemp[1];
		caseVertex[6] = caseVertexTemp[2];
		caseVertex[5] = caseVertexTemp[6];
		caseVertex[1] = caseVertexTemp[5];

		int flagIndex = 0;
		for (int i = 0; i < 8; i++) {
			if (caseVertex[i]) flagIndex |= 1 << i;
		}

		return flagIndex;
	}

	public int ReverseX(int number) {
		for (int j = 0; j < 8; j++) {
			caseVertexTemp[j] = (number & (1 << j)) == (1 << j);
		}

		caseVertex[3] = caseVertexTemp[0];
		caseVertex[0] = caseVertexTemp[1];
		caseVertex[1] = caseVertexTemp[2];
		caseVertex[2] = caseVertexTemp[3];

		caseVertex[7] = caseVertexTemp[4];
		caseVertex[4] = caseVertexTemp[5];
		caseVertex[5] = caseVertexTemp[6];
		caseVertex[6] = caseVertexTemp[7];

		int flagIndex = 0;
		for (int i = 0; i < 8; i++) {
			if (caseVertex[i]) flagIndex |= 1 << i;
		}

		return flagIndex;
	}

	public int ReverseY(int number) {
		for (int j = 0; j < 8; j++) {
			caseVertexTemp[j] = (number & (1 << j)) == (1 << j);
		}

		caseVertex[0] = caseVertexTemp[3];
		caseVertex[3] = caseVertexTemp[7];
		caseVertex[7] = caseVertexTemp[4];
		caseVertex[4] = caseVertexTemp[0];

		caseVertex[1] = caseVertexTemp[2];
		caseVertex[2] = caseVertexTemp[6];
		caseVertex[6] = caseVertexTemp[5];
		caseVertex[5] = caseVertexTemp[1];

		int flagIndex = 0;
		for (int i = 0; i < 8; i++) {
			if (caseVertex[i]) flagIndex |= 1 << i;
		}

		return flagIndex;
	}
	
	public int[] rotListX = {
		3, //0,
		0, //1,
		1, //2,
	    2, //3,
		7, //4,
		4, //5,
		5, //6,
		6, //7,
		11,//8,
		8, //9,
		9, //10,
		10,//11
	};

	public int[] rotListY = {
		4, //0,
		9, //1,
		0, //2,
		8, //3,
		6, //4,
		10, //5,
		2, //6,
		11, //7,
		7, //8,
		5, //9,
		1, //10,
		3, //11
	};
	
	public int[] rotPtListX = {
		6, //0,
		3, //1,
		0, //2,
		7, //3,
		4, //4,
		1, //5,
		8, //6,
		5, //7,
		2, //8,
		15,//9,
		12,//10,
		9, //11,
		16,//12,
		13,//13,
		10,//14,
		17,//15,
		14,//16,
		11,//17,
		24,//18,
		21,//19,
		18,//20,
		25,//21,
		22,//22,
		19,//23,
		26,//24,
		23,//25,
		20,//26
	};
	
	public int[] rotPtListY = {
		18,//0,
		19,//1,
		20,//2,
		9, //3,
		10,//4,
		11,//5,
		0, //6,
		1, //7,
		2, //8,
		21,//9,
		22,//10,
		23,//11,
		12,//12,
		13,//13,
		14,//14,
		3, //15,
		4, //16,
		5, //17,
		24,//18,
		25,//19,
		26,//20,
		15,//21,
		16,//22,
		17,//23,
		6, //24,
		7, //25,
		8, //26
	};

	public List<int> caseIndex = new List<int>();
	public List<int> storedIndex = new List<int>();
	public List<int> rotateIndex = new List<int>();


	public bool[] caseVertex = new bool[8];
	public bool[] caseVertexTemp = new bool[8];

	public int GetOriginIndex(int flagIndex) {
		return storedIndex[flagIndex];
	}

	public int GetOriginRotation(int flagIndex) {
		return rotateIndex[flagIndex];
	}

	public int RotateFlag(int flag, int rot) {
		if (rot == 0) return flag;
		if (rot == 1) return rotX(flag);
		if (rot == 2) return rotX(rotX(flag));
		if (rot == 3) return rotX(rotX(rotX(flag)));
		
		if (rot == 4) return rotY(flag);
		if (rot == 5) return rotX(rotY(flag));
		if (rot == 6) return rotX(rotX(rotY(flag)));
		if (rot == 7) return rotX(rotX(rotX(rotY(flag))));
		
		if (rot == 8) return rotY(rotY(flag));
		if (rot == 9) return rotX(rotY(rotY(flag)));
		if (rot == 10) return rotX(rotX(rotY(rotY(flag))));
		if (rot == 11) return rotX(rotX(rotX(rotY(rotY(flag)))));
		
		if (rot == 12) return rotY(rotY(rotY(flag)));
		if (rot == 13) return rotX(rotY(rotY(rotY(flag))));
		if (rot == 14) return rotX(rotX(rotY(rotY(rotY(flag)))));
		if (rot == 15) return rotX(rotX(rotX(rotY(rotY(rotY(flag))))));
		
		if (rot == 16) return rotY(rotX(flag));
		if (rot == 17) return rotX(rotY(rotX(flag)));
		if (rot == 18) return rotX(rotX(rotY(rotX(flag))));
		if (rot == 19) return rotX(rotX(rotX(rotY(rotX(flag)))));
		
		if (rot == 20) return rotY(rotX(rotX(rotX(flag))));
		if (rot == 21) return rotX(rotY(rotX(rotX(rotX(flag)))));
		if (rot == 22) return rotX(rotX(rotY(rotX(rotX(rotX(flag))))));
		if (rot == 23) return rotX(rotX(rotX(rotY(rotX(rotX(rotX(flag)))))));
		
		return flag;
	}

	public int ReverseRotateFlag(int flag, int rot) {
		if (rot == 0) return flag;
		if (rot == 1) return ReverseX(flag);
		if (rot == 2) return ReverseX(ReverseX(flag));
		if (rot == 3) return ReverseX(ReverseX(ReverseX(flag)));
		
		if (rot == 4) return ReverseY(flag);
		if (rot == 5) return ReverseY(ReverseX(flag));
		if (rot == 6) return ReverseY(ReverseX(ReverseX(flag)));
		if (rot == 7) return ReverseY(ReverseX(ReverseX(ReverseX(flag))));
		
		if (rot == 8) return ReverseY(ReverseY(flag));
		if (rot == 9) return ReverseY(ReverseY(ReverseX(flag)));
		if (rot == 10) return ReverseY(ReverseY(ReverseX(ReverseX(flag))));
		if (rot == 11) return ReverseY(ReverseY(ReverseX(ReverseX(ReverseX(flag)))));
		
		if (rot == 12) return ReverseY(ReverseY(ReverseY(flag)));
		if (rot == 13) return ReverseY(ReverseY(ReverseY(ReverseX(flag))));
		if (rot == 14) return ReverseY(ReverseY(ReverseY(ReverseX(ReverseX(flag)))));
		if (rot == 15) return ReverseY(ReverseY(ReverseY(ReverseX(ReverseX(ReverseX(flag))))));
		
		if (rot == 16) return ReverseX(ReverseY(flag));
		if (rot == 17) return ReverseX(ReverseY(ReverseX(flag)));
		if (rot == 18) return ReverseX(ReverseY(ReverseX(ReverseX(flag))));
		if (rot == 19) return ReverseX(ReverseY(ReverseX(ReverseX(ReverseX(flag)))));
		
		if (rot == 20) return ReverseX(ReverseX(ReverseX(ReverseY(flag))));
		if (rot == 21) return ReverseX(ReverseX(ReverseX(ReverseY(ReverseX(flag)))));
		if (rot == 22) return ReverseX(ReverseX(ReverseX(ReverseY(ReverseX(ReverseX(flag))))));
		if (rot == 23) return ReverseX(ReverseX(ReverseX(ReverseY(ReverseX(ReverseX(ReverseX(flag)))))));
		return flag;
	}

	public int RotateCubePoint(int point, int rot) {
		if (point == -1) return -1;
		
		if (rot == 0) return point;
		if (rot == 1) return rotPtListX[point];
		if (rot == 2) return rotPtListX[rotPtListX[point]];
		if (rot == 3) return rotPtListX[rotPtListX[rotPtListX[point]]];
			
		if (rot == 4) return rotPtListY[point];
		if (rot == 5) return rotPtListY[rotPtListX[point]];
		if (rot == 6) return rotPtListY[rotPtListX[rotPtListX[point]]];
		if (rot == 7) return rotPtListY[rotPtListX[rotPtListX[rotPtListX[point]]]];
		
		if (rot == 8) return rotPtListY[rotPtListY[point]];
		if (rot == 9) return rotPtListY[rotPtListY[rotPtListX[point]]];
		if (rot == 10) return rotPtListY[rotPtListY[rotPtListX[rotPtListX[point]]]];
		if (rot == 11) return rotPtListY[rotPtListY[rotPtListX[rotPtListX[rotPtListX[point]]]]];
			
		if (rot == 12) return rotPtListY[rotPtListY[rotPtListY[point]]];
		if (rot == 13) return rotPtListY[rotPtListY[rotPtListY[rotPtListX[point]]]];
		if (rot == 14) return rotPtListY[rotPtListY[rotPtListY[rotPtListX[rotPtListX[point]]]]];
		if (rot == 15) return rotPtListY[rotPtListY[rotPtListY[rotPtListX[rotPtListX[rotPtListX[point]]]]]];
			
		if (rot == 16) return rotPtListX[rotPtListY[point]];
		if (rot == 17) return rotPtListX[rotPtListY[rotPtListX[point]]];
		if (rot == 18) return rotPtListX[rotPtListY[rotPtListX[rotPtListX[point]]]];
		if (rot == 19) return rotPtListX[rotPtListY[rotPtListX[rotPtListX[rotPtListX[point]]]]];

		if (rot == 20) return rotPtListX[rotPtListX[rotPtListX[rotPtListY[point]]]];
		if (rot == 21) return rotPtListX[rotPtListX[rotPtListX[rotPtListY[rotPtListX[point]]]]];
		if (rot == 22) return rotPtListX[rotPtListX[rotPtListX[rotPtListY[rotPtListX[rotPtListX[point]]]]]];
		if (rot == 23) return rotPtListX[rotPtListX[rotPtListX[rotPtListY[rotPtListX[rotPtListX[rotPtListX[point]]]]]]];

		return point;
	}

	public int RotateEdgePoint(int point, int rot) {
		if (point == -1) return -1;
		
		if (rot == 0) return point;
		if (rot == 1) return rotListX[point];
		if (rot == 2) return rotListX[rotListX[point]];
		if (rot == 3) return rotListX[rotListX[rotListX[point]]];
			
		if (rot == 4) return rotListY[point];
		if (rot == 5) return rotListY[rotListX[point]];
		if (rot == 6) return rotListY[rotListX[rotListX[point]]];
		if (rot == 7) return rotListY[rotListX[rotListX[rotListX[point]]]];
		
		if (rot == 8) return rotListY[rotListY[point]];
		if (rot == 9) return rotListY[rotListY[rotListX[point]]];
		if (rot == 10) return rotListY[rotListY[rotListX[rotListX[point]]]];
		if (rot == 11) return rotListY[rotListY[rotListX[rotListX[rotListX[point]]]]];
			
		if (rot == 12) return rotListY[rotListY[rotListY[point]]];
		if (rot == 13) return rotListY[rotListY[rotListY[rotListX[point]]]];
		if (rot == 14) return rotListY[rotListY[rotListY[rotListX[rotListX[point]]]]];
		if (rot == 15) return rotListY[rotListY[rotListY[rotListX[rotListX[rotListX[point]]]]]];
			
		if (rot == 16) return rotListX[rotListY[point]];
		if (rot == 17) return rotListX[rotListY[rotListX[point]]];
		if (rot == 18) return rotListX[rotListY[rotListX[rotListX[point]]]];
		if (rot == 19) return rotListX[rotListY[rotListX[rotListX[rotListX[point]]]]];

		if (rot == 20) return rotListX[rotListX[rotListX[rotListY[point]]]];
		if (rot == 21) return rotListX[rotListX[rotListX[rotListY[rotListX[point]]]]];
		if (rot == 22) return rotListX[rotListX[rotListX[rotListY[rotListX[rotListX[point]]]]]];
		if (rot == 23) return rotListX[rotListX[rotListX[rotListY[rotListX[rotListX[rotListX[point]]]]]]];
		
		return point;
	}

	public Vector3 CalculateNormal(Vector3 p1, Vector3 p2, Vector3 p3) {
		Vector3 A = p2 - p1;
		Vector3 B = p3 - p1;

		float Nx = A.y * B.z - A.z * B.y;
		float Ny = A.z * B.x - A.x * B.z;
		float Nz = A.x * B.y - A.y * B.x;
	    
		return new Vector3(Nx, Ny, Nz).normalized;
	}

	public bool GetTriangleList(int caseId, int rot, int negFlagIndex, int[] triangles, Vector3[] normals, 
		Vector3[] norms, Vector3[] edgeVertex) {
		
		negFlagIndex = RotateFlag(negFlagIndex, rot);
		
		bool[] totalP = new bool[8];
		for (int i = 0; i < 8; i++) {
			totalP[i] = (negFlagIndex & (1 << i)) == (1 << i);
		}

		bool affected = false;
		int allSize = 0;
		for (int i = 0; i < triangleList.GetLength(1); i += 2) {
			int edge1 = triangleList[caseId, i];
			int edge2 = triangleList[caseId, i + 1];
			if (edge1 == -1) break;
			
			bool e1 = (totalP[EdgeConnection[edge1, 0]] || totalP[EdgeConnection[edge1, 1]]);
			bool e2 = (totalP[EdgeConnection[edge2, 0]] || totalP[EdgeConnection[edge2, 1]]);

			if (e1 && e2) {
				affected = true;
				continue;
			} else if (e1) {
				edge1 = edgeShift[edge1, edge2] + 12;
				affected = true;
				
			} else if (e2) {
				edge2 = edgeShift[edge2, edge1] + 12;
				affected = true;
				
			}
			
			if (!e1 && !e2 && i + 5 < triangleList.GetLength(1) && triangleList[caseId, i + 5] > -1) {
				int edge3 = triangleList[caseId, i + 2];
				int edge4 = triangleList[caseId, i + 3];
				int edge5 = triangleList[caseId, i + 4];
				int edge6 = triangleList[caseId, i + 5];
				bool e3 = (totalP[EdgeConnection[edge3, 0]] || totalP[EdgeConnection[edge3, 1]]);
				bool e5 = (totalP[EdgeConnection[edge5, 0]] || totalP[EdgeConnection[edge5, 1]]);
				if (edge2 == edge3 && edge4 == edge5 && edge6 == edge1) {
					if (!e1 && !e3 && !e5) {
						triangles[allSize++] = RotateEdgePoint(edge1, rot);
						triangles[allSize++] = RotateEdgePoint(edge3, rot);
						triangles[allSize++] = RotateEdgePoint(edge5, rot);
						i += 4;
						continue;
					}
				} else if (i + 7 < triangleList.GetLength(1) && triangleList[caseId, i + 7] > -1) {
					int edge7 = triangleList[caseId, i + 6];
					int edge8 = triangleList[caseId, i + 7];
					bool e7 = (totalP[EdgeConnection[edge7, 0]] || totalP[EdgeConnection[edge7, 1]]);
					if (edge2 == edge3 && edge4 == edge5 && edge6 == edge7 && edge8 == edge1) {
						if (!e1 && !e3 && !e5 && !e7) {
							triangles[allSize++] = RotateEdgePoint(edge1, rot);
							triangles[allSize++] = RotateEdgePoint(edge3, rot);
							triangles[allSize++] = RotateEdgePoint(edge5, rot);
							
							triangles[allSize++] = RotateEdgePoint(edge1, rot);
							triangles[allSize++] = RotateEdgePoint(edge5, rot);
							triangles[allSize++] = RotateEdgePoint(edge7, rot);
							i += 6;
							continue;
						}
					}
				}
			}
			
			triangles[allSize++] = 13 + 12;
			triangles[allSize++] = edge1 < 12 ? RotateEdgePoint(edge1, rot) : RotateCubePoint(edge1 - 12, rot) + 12;
			triangles[allSize++] = edge2 < 12 ? RotateEdgePoint(edge2, rot) : RotateCubePoint(edge2 - 12, rot) + 12;
		}
		triangles[allSize] = -1;
		Vector3 total = new Vector3();
		int first = -1;
		for (int i = 0; i < triangles.Length; i += 3) {
			if (triangles[i] < 0) break;
			
			Vector3 norm = CalculateNormal(
				edgeVertex[triangles[i]],
				edgeVertex[triangles[i + 1]],
				edgeVertex[triangles[i + 2]]);
			
			if (triangles[i] == 13 + 12) total += norm;
			
			if (triangles[i] >= 12) {
				normals[i] = norm;
			} else {
				normals[i] = norms[triangles[i]];
			}

			if (triangles[i + 1] >= 12) {
				normals[i + 1] = norm;
			} else {
				normals[i + 1] = norms[triangles[i + 1]];
			}

			if (triangles[i + 2] >= 12) {
				normals[i + 2] = norm;
			} else {
				normals[i + 2] = norms[triangles[i + 2]];
			}
		}

		total = total.normalized;
		for (int i = 0; i < triangles.Length; i += 3) {
			if (triangles[i] < 0) break;
			if (triangles[i] == 13 + 12) {
				normals[i] = total;
			}
		}

		return affected;
	}

	public void AddStoreRot(int store, int rot) {
		/*if (store == 21) {
			storedIndex.Add(0);
			
		} else if (store == 16) {
			storedIndex.Add(1);
			
		} else if (store == 5) {
			storedIndex.Add(2);
			
		} else if (store == 19) {
			storedIndex.Add(3);
			
		} else {
			storedIndex.Add(-1);
			
		}*/

		storedIndex.Add(store);
		rotateIndex.Add(rot);
	}

	public int[][] ComputeTriangles(int sourcePoint, int[] triangleList, out int mask) {
		// usa 256 pontos negativos possíveis
		// se um ponto negativo afetar pelo menos 1 triangulo, salva o valor
		// depois de todos os valores afetarem, verifica a mascara, e cria os subsequentes
		// consumir 1 ponto : mover, para H ou V ? Depende se é o primeiro ou ultimo ponto do triangulo
		// consumir 2 pontos (por 1 ou mais pontos negativos) : remover
		
		// Source Points
		bool[] points = new bool[8];
		for (int i = 0; i < 8; i++) {
			points[i] = (sourcePoint & (1 << i)) == (1 << i);
		}

		// List all negative that affect the source triangles
		int total = 1;
		int negMask = 0;
		bool[] negAffect = new bool[8];
		for (int i = 0; i < 8; i++) {
			if (points[i]) {
				for (int j = 0; j < triangleList.Length; j ++) {
					int edge = triangleList[j];
					if (EdgeConnection[edge, 0] == i || EdgeConnection[edge, 1] == i) {
						negAffect[i] = true;
						break;
					}
				}

				if (negAffect[i]) {
					negMask |= 1 << i;
					total *= 2;
				}
			}
		}
		mask = negMask;
		Debug.Log("Total cases : " + (total - 1));
		
		int[][] allCases = new int[total][];
		for (int index = 0; index < total; index++) {
			bool[] totalP = new bool[8];
			int p = 0;
			
			for (int j = 0; j < 8; j++) {
				int n = 0;
				if ((index & (1 << j)) == (1 << j)) {
					for (int k = 0; k < 8; k++) {
						if (negAffect[k]) {
							if (n == j) {
								totalP[k] = true;
								p |= 1 << k;
								break;
							}

							n++;
						}

					}
				}
			}
			
			allCases[index] = new int[triangleList.Length + 1];
			int allSize = 0;
			for (int i = 0; i < triangleList.Length; i += 2) {
				int edge1 = triangleList[i];
				int edge2 = triangleList[i + 1];
				bool iEdge1 = (totalP[EdgeConnection[edge1, 0]] || totalP[EdgeConnection[edge1, 1]]);
				bool iEdge2 = (totalP[EdgeConnection[edge2, 0]] || totalP[EdgeConnection[edge2, 1]]);
				if (iEdge1 && iEdge2) {
					continue;
				} 
				
				if (iEdge1) {
					edge1 = edgeShift[edge1, edge2] + 12;
				} else if (iEdge2) {
					edge2 = edgeShift[edge2, edge1] + 12;
				}
				allCases[index][allSize++] = edge1;
				allCases[index][allSize++] = edge2;
				
			}
			allCases[index][allSize] = -1;
			
			Debug.Log("	Case " + index + " is " + Convert.ToString(p, 2) + " with " + string.Join(", ", totalP) + " and " + (allSize / 2) + " triangles ");
		}
		return allCases;
	}

	public int[,] edgeShift = {
		{ // 0
			4,//0
			4, //1
			4, //2
			4, //3
			10,//4
			0,//5
			0,//6
			0,//7
			10,//8
			10,//9
			0,//10
			0,//11
		},
		{ // 1
			4, //0
			4, //1
			4, //2
			4, //3
			0,//4
			14,//5
			0,//6
			0,//7
			0,//8
			14,//9
			14,//10
			0,//11
		},
		{ // 2
			4, //0
			4, //1
			4, //2
			4, //3
			0,//4
			0,//5
			16,//6
			0,//7
			0,//8
			0,//9
			16,//10
			16,//11
		},
		{ // 3
			4, //0
			4, //1
			4, //2
			4, //3
			0,//4
			0,//5
			0,//6
			12,//7
			12,//8
			0,//9
			0,//10
			12,//11
		},
		{ // 4
			10, //0
			0, //1
			0, //2
			0, //3
			22,//4
			22,//5
			22,//6
			22,//7
			10,//8
			10,//9
			0,//10
			0,//11
		},
		{ // 5
			0, //0
			14,//1
			0, //2
			0, //3
			22,//4
			22,//5
			22,//6
			22,//7
			0, //8
			14,//9
			14,//10
			0, //11
		},
		{ // 6
			0, //0
			0, //1
			16, //2
			0, //3
			22,//4
			22,//5
			22,//6
			22,//7
			0, //8
			0, //9
			16,//10
			16,//11
		},
		{ // 7
			0, //0
			0, //1
			0, //2
			12, //3
			22,//4
			22,//5
			22,//6
			22,//7
			12, //8
			0, //9
			0,//10
			12,//11
		},
		{ // 8
			10, //0
			0, //1
			0, //2
			12, //3
			10,//4
			0,//5
			0,//6
			12,//7
			12, //8
			10, //9
			0,//10
			12,//11
		},
		{ // 9
			10, //0
			14, //1
			0, //2
			0, //3
			10,//4
			14,//5
			0,//6
			0,//7
			10, //8
			10, //9
			14,//10
			0,//11
		},
		{ // 10
			0, //0
			14, //1
			16, //2
			0, //3
			0,//4
			14,//5
			16,//6
			0,//7
			0, //8
			14, //9
			16,//10
			16,//11
		},
		{ // 11
			0, //0
			0, //1
			16, //2
			12, //3
			0,//4
			0,//5
			16,//6
			12,//7
			12, //8
			0, //9
			16,//10
			16,//11
		}
	};

	public int[,] triangleList = {
		{-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{0, 8, 8, 3, 3, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{3, 1, 1, 9, 9, 8, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{0, 8, 8, 3, 3, 0, 10, 1, 1, 2, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{2, 10, 10, 9, 9, 8, 8, 3, 3, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{8, 11, 11, 10, 10, 9, 9, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{2, 10, 10, 1, 1, 2, 8, 4, 4, 7, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{7, 3, 3, 0, 0, 4, 4, 7, 2, 10, 10, 1, 1, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{2, 10, 10, 9, 9, 4, 4, 7, 7, 3, 3, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{0, 1, 1, 9, 9, 0, 3, 11, 11, 2, 2, 3, 8, 4, 4, 7, 7, 8, -1, -1, -1, -1, -1, -1},
		{2, 1, 1, 9, 9, 4, 4, 7, 7, 11, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{0, 4, 4, 7, 7, 11, 11, 10, 10, 1, 1, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{10, 9, 9, 0, 0, 3, 3, 11, 11, 10, 8, 4, 4, 7, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1},
		{10, 9, 9, 4, 4, 7, 7, 11, 11, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{10, 1, 1, 3, 3, 11, 11, 10, 5, 7, 7, 8, 8, 9, 9, 5, -1, -1, -1, -1, -1, -1, -1, -1},
		{9, 5, 5, 7, 7, 11, 11, 10, 10, 1, 1, 0, 0, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{10, 5, 5, 7, 7, 11, 11, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{2, 3, 3, 11, 11, 2, 6, 5, 5, 10, 10, 6, 1, 9, 9, 0, 0, 1, 8, 4, 4, 7, 7, 8},
		{2, 1, 1, 9, 9, 4, 4, 7, 7, 11, 11, 2, 6, 5, 5, 10, 10, 6, -1, -1, -1, -1, -1, -1},
		{6, 5, 5, 9, 9, 4, 4, 7, 7, 11, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{6, 7, 11, 6, 7, 11, 0, 9, 1, 0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{6, 7, 7, 11, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}
	};
	    
	public void GetCases() {
		for (int i = 0; i < 256; i++) {
			int px0 = i;
			int px1 = rotX(px0);
			int px2 = rotX(px1);
			int px3 = rotX(px2);

			int py1x0 = rotY(px0);
			int py1x1 = rotX(py1x0);
			int py1x2 = rotX(py1x1);
			int py1x3 = rotX(py1x2);

			int py2x0 = rotY(py1x0);
			int py2x1 = rotX(py2x0);
			int py2x2 = rotX(py2x1);
			int py2x3 = rotX(py2x2);

			int py3x0 = rotY(py2x0);
			int py3x1 = rotX(py3x0);
			int py3x2 = rotX(py3x1);
			int py3x3 = rotX(py3x2);

			int pz1x0 = rotY(rotX(i));
			int pz1x1 = rotX(pz1x0);
			int pz1x2 = rotX(pz1x1);
			int pz1x3 = rotX(pz1x2);

			int pz2x0 = rotY(rotX(rotX(rotX(i))));
			int pz2x1 = rotX(pz2x0);
			int pz2x2 = rotX(pz2x1);
			int pz2x3 = rotX(pz2x2);

			int store = caseIndex.IndexOf(px0);
			if (store != -1) AddStoreRot(store, 0); // must be impossible
			else if ((store = caseIndex.IndexOf(px1)) != -1) AddStoreRot(store, 1);
			else if ((store = caseIndex.IndexOf(px2)) != -1) AddStoreRot(store, 2);
			else if ((store = caseIndex.IndexOf(px3)) != -1) AddStoreRot(store, 3);
			else if ((store = caseIndex.IndexOf(py1x0)) != -1) AddStoreRot(store, 4);
			else if ((store = caseIndex.IndexOf(py1x1)) != -1) AddStoreRot(store, 5);
			else if ((store = caseIndex.IndexOf(py1x2)) != -1) AddStoreRot(store, 6);
			else if ((store = caseIndex.IndexOf(py1x3)) != -1) AddStoreRot(store, 7);
			else if ((store = caseIndex.IndexOf(py2x0)) != -1) AddStoreRot(store, 8);
			else if ((store = caseIndex.IndexOf(py2x1)) != -1) AddStoreRot(store, 9);
			else if ((store = caseIndex.IndexOf(py2x2)) != -1) AddStoreRot(store, 10);
			else if ((store = caseIndex.IndexOf(py2x3)) != -1) AddStoreRot(store, 11);
			else if ((store = caseIndex.IndexOf(py3x0)) != -1) AddStoreRot(store, 12);
			else if ((store = caseIndex.IndexOf(py3x1)) != -1) AddStoreRot(store, 13);
			else if ((store = caseIndex.IndexOf(py3x2)) != -1) AddStoreRot(store, 14);
			else if ((store = caseIndex.IndexOf(py3x3)) != -1) AddStoreRot(store, 15);
			else if ((store = caseIndex.IndexOf(pz1x0)) != -1) AddStoreRot(store, 16);
			else if ((store = caseIndex.IndexOf(pz1x1)) != -1) AddStoreRot(store, 17);
			else if ((store = caseIndex.IndexOf(pz1x2)) != -1) AddStoreRot(store, 18);
			else if ((store = caseIndex.IndexOf(pz1x3)) != -1) AddStoreRot(store, 19);
			else if ((store = caseIndex.IndexOf(pz2x0)) != -1) AddStoreRot(store, 20);
			else if ((store = caseIndex.IndexOf(pz2x1)) != -1) AddStoreRot(store, 21);
			else if ((store = caseIndex.IndexOf(pz2x2)) != -1) AddStoreRot(store, 22);
			else if ((store = caseIndex.IndexOf(pz2x3)) != -1) AddStoreRot(store, 23);
			else {
				AddStoreRot(caseIndex.Count, 0);
				caseIndex.Add(i);
			}

		}
	}

	public int[,] caseFlags = {
		{
			0b01011000, 
			0b00001000,  1, 
			0b00010000,  2, 
			0b01000000,  3, 
			0b00011000,  4, 
			0b01001000,  5, 
			0b01010000,  6, 
			0b01011000,  0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1
		},
		{
			0b00111100, 
			0b00010000,  7, 
			0b00100000,  8, 
			0b00110000,  9,
			0b00001000, 10, 
			0b00000100, 11,
			0b00001100, 12, 
			0b00011000, 13, 
			0b00101000, 14, 
			0b00111000, 15, 
			0b00010100, 16, 
			0b00100100, 17, 
			0b00110100, 18, 
			0b00011100, 19, 
			0b00101100, 20, 
			0b00111100,  0
		},
		{
			0b00001111, 
			0b00000001, 21, 
			0b00000010, 22, 
			0b00000011, 23, 
			0b00000100, 24, 
			0b00000101, 25, 
			0b00000110, 26, 
			0b00000111, 27, 
			0b00001000, 28, 
			0b00001001, 29, 
			0b00001010, 30,  
			0b00001011, 31,   
			0b00001100, 32,   
			0b00001101, 33,    
			0b00001110, 34,    
			0b00001111, 0,
		},
		{
			0b11111111, 
			0b00000010, 35, 
			0b00001000, 36,  
			0b00001010, 37,  
			0b00010000, 38,  
			0b00010010, 39, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1
		},
	};

	// todo - remover o ponto 13, e usalo automaticamente em todos
	public int[,] caseTriangles = {
		{-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}, 
		// 1
		{6, 7, 7, 12+12, 16+12, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{6, 22+12, 11, 6, 12+12, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{11, 16+12, 7, 11, 22+12, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{6, 22+12, 16+12, 6, -1, -1, -1, 1-1, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{7, 12+12, 22+12, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},  
		{12+12, 11, 11, 16+12, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		// 7
		{12+12, 11, 11, 10, 10,  5, 5, 22+12, -1, -1, -1, -1, -1, -1, -1, -1},
		{7, 11, 11, 10, 10, 14+12, 22+12, 7, -1, -1, -1, -1, -1, -1, -1, -1},
		{12+12, 11, 11, 10, 10, 14+12, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{7, 12+12, 5, 7, 10, 5, 16+12, 10, -1, -1, -1, -1, -1, -1, -1, -1},
		{7, 11, 11, 16+12, 5, 7, 14+12, 5, -1, -1, -1, -1, -1, -1, -1, -1},
		{7, 12+12, 5, 7, 14+12, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{16+12, 10, 5, 22+12, 10, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{10, 14+12, 16+12, 10, 7, 12+12, 22+12, 7, -1, -1, -1, -1, -1, -1, -1, -1},
		{10, 14+12, 16+12, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{12+12, 11, 11, 16+12, 5, 22+12, 14+12, 5, -1, -1, -1, -1, -1, -1, -1, -1},
		{22+12, 7, 7, 11, 11, 16+12, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{12+12, 11, 11, 16+12, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{14+12, 5, 5, 22+12, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{7, 12+12, 22+12, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}, 
		// 21
		{10, 9, 9, 10+12, 11, 10, 12+12, 11, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{10+12, 8, 8, 11, 11, 10, 10, 14+12, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{12+12, 11, 11, 10, 10, 14+12, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{14+12, 9, 9, 8, 8, 11, 11, 16+12, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{14+12, 9, 9, 10+12, 12+12, 11, 11, 16+12, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{10+12, 8, 8, 11, 11, 16+12, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{12+12, 11, 11, 16+12, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{8, 12+12, 9, 8, 10, 9, 16+12, 10, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{16+12, 10, 10, 9, 9, 10+12, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{16+12, 10, 10, 14+12, 10+12, 8, 8, 12+12, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{16+12, 10, 10, 14+12, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{14+12, 9, 9, 8, 8, 12+12, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}, 
		{14+12, 9, 9, 10+12, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		{10+12, 8, 8, 12+12, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
		// 35
		// {5, 9, 6, 5, 11, 6, 7, 11, 4, 7, 9, 4, -1, -1, -1, -1}, 
		{5, 14+12, 6, 5, 11, 6, 7, 11, 4, 7, 10+12, 4, -1, -1, -1, -1},
		{5, 9, 6, 5, 16+12, 6, 7, 12+12, 4, 7, 9, 4, -1, -1, -1, -1}, 
		{5, 14+12, 6, 5, 16+12, 6, 7, 12+12, 4, 7, 10+12, 4, -1, -1, -1, -1}, 
		{5, 9, 6, 5, 11, 6, 12+12, 11, 9, 10+12, -1, -1, -1, -1, -1, -1}, 
		{5, 9, 6, 5, 11, 6, 7, 11, 4, 7, 9, 4, -1, -1, -1, -1}, 
	};
	// {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}, 
}