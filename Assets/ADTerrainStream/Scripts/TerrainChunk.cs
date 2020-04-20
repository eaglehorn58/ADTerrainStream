//	Copyright(c) 2020, Andy Do
//	eaglehorn58@gmail.com, eaglehorn58@163.com

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ADTerrainStream
{
	public class TerrainChunk
	{
		public int row { get; } = -1;
		public int col { get; } = -1;
		public bool inLoadingList { get; set; } = false;
		public bool cancelLoading { get; set; } = false;

		private List<Vector3> pos = null;       //	position data
		private List<Vector2> uvs = null;       //	uv data
		private List<int> triangles = null;     //	indices data

		private GameObject gameObj = null;
		private Mesh chunkMesh = null;

		public TerrainChunk(int r, int c)
		{
			row = r;
			col = c;
		}

		//	fill chunk data, this function is called from loading thread
		public void FillData(float[] posBuf, int numVerts, int chunkGrid)
		{
			try
			{
				pos = new List<Vector3>();
				uvs = new List<Vector2>();
				triangles = new List<int>();

				int indexVert = 0;

				for (int r = 0; r <= chunkGrid; r++)
				{
					float v = (float)r / chunkGrid;

					for (int c = 0; c <= chunkGrid; c++)
					{
						int off = indexVert * 3;
						pos.Add(new Vector3(posBuf[off], posBuf[off + 1], posBuf[off + 2]));

						float u = (float)c / chunkGrid;
						uvs.Add(new Vector2(u, v));

						if (r < chunkGrid && c < chunkGrid)
						{
							triangles.Add(indexVert);
							triangles.Add(indexVert + 1);
							triangles.Add(indexVert + chunkGrid + 2);

							triangles.Add(indexVert);
							triangles.Add(indexVert + chunkGrid + 2);
							triangles.Add(indexVert + chunkGrid + 1);
						}

						indexVert++;
					}
				}
			}
			catch
			{
				Debug.Log("TerrainChunk.FillData failed");
			}
		}

		//	generate unity objects, this function should be called from main thread
		public void GenerateObjects(Material mat)
		{
			try
			{
				if (pos.Count == 0)
				{
					Debug.Log("TerrainChunk.GenerateObjects, chunk data hasn't been filled");
					Debug.Assert(false);
					return;
				}

				//	create gameobject
				gameObj = new GameObject
				{
					name = "trn_chunk_" + row.ToString() + "_" + col.ToString(),
				};

				gameObj.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
				gameObj.AddComponent<MeshFilter>();
				gameObj.AddComponent<MeshRenderer>();

				//	create mesh
				chunkMesh = new Mesh();
				chunkMesh.vertices = pos.ToArray();
				chunkMesh.uv = uvs.ToArray();
				chunkMesh.SetTriangles(triangles.ToArray(), 0);
				chunkMesh.RecalculateNormals();
				chunkMesh.RecalculateBounds();
				gameObj.GetComponent<MeshFilter>().mesh = chunkMesh;

				//	material
				gameObj.GetComponent<MeshRenderer>().material = mat;
			}
			catch
			{
				Debug.Log("TerrainChunk.GenerateObjects failed");
			}
		}

		public void DestroyChunk()
		{
			if (chunkMesh)
			{
				Object.Destroy(chunkMesh);
				chunkMesh = null;
			}

			if (gameObj != null)
			{
				Object.Destroy(gameObj);
				gameObj = null;
			}
		}
	}
}
