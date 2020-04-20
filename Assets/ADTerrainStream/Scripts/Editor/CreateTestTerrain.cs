//	Copyright(c) 2020, Andy Do
//	eaglehorn58@gmail.com

using System.Collections;
using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace ADTerrainStream
{
	public class GenTestTerrainWnd : EditorWindow
	{
		[MenuItem("Tools/AD Terrain Stream")]
		static void Init()
		{
			var window = EditorWindow.GetWindow(typeof(GenTestTerrainWnd)) as GenTestTerrainWnd;
			window.position = new Rect(new Vector2(300, 300), new Vector2(300, 100));
			window.maxSize = new Vector2(300, 100);
			window.Show();
		}

		void OnGUI()
		{
			GUILayout.Label("Generate Test Terrain", EditorStyles.boldLabel);

			if (GUILayout.Button("Start"))
			{
				if (GenerateTerrain() == true)
				{
					EditorUtility.DisplayDialog("Message", "Succeeded!", "Ok");
				}
				else
				{
					EditorUtility.DisplayDialog("Message", "Failed!", "Ok");
				}
			}
		}

		bool GenerateTerrain()
		{
			const int hmWid = 4097;		//	height map size
			const int widthInGrids = hmWid - 1;
			const int chunkWid = 32;	//	chunk width in grids
			const float gridSize = 1.0f;

			const float sx = -(widthInGrids / 2) * gridSize;
			const float sz = -sx;
			const int widthInChunk = widthInGrids / chunkWid;

			string fileName = Application.dataPath + "/ADTerrainStream/Terrain.data";

			try
			{
				using (BinaryWriter bw = new BinaryWriter(File.Create(fileName)))
				{
					//	write file head
					bw.Write(hmWid);
					bw.Write(widthInGrids);
					bw.Write(chunkWid);
					bw.Write(gridSize);

					const int chunkVertNum = (chunkWid + 1) * (chunkWid + 1);
					const int floatCount = chunkVertNum * 3;
					float[] vertBuf = new float[floatCount];
					byte[] dataBuf = new byte[floatCount * sizeof(float)];

					for (int r = 0; r < widthInChunk; r++)
					{
						for (int c = 0; c < widthInChunk; c++)
						{
							float sx1 = sx + c * chunkWid * gridSize;
							float sz1 = sz - r * chunkWid * gridSize;
							int vertBufIdx = 0;

							for (int vr = 0; vr <= chunkWid; vr++)
							{
								for (int vc = 0; vc <= chunkWid; vc++)
								{
									float x = sx1 + vc * gridSize;
									float z = sz1 - vr * gridSize;
									vertBuf[vertBufIdx++] = x;
									vertBuf[vertBufIdx++] = Mathf.Sin(x * 0.01f) * Mathf.Sin(z * 0.012f) * 50.0f;
									vertBuf[vertBufIdx++] = z;
								}
							}

							Buffer.BlockCopy(vertBuf, 0, dataBuf, 0, floatCount * sizeof(float));
							bw.Write(dataBuf, 0, floatCount * sizeof(float));
						}
					}
				}

				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}


