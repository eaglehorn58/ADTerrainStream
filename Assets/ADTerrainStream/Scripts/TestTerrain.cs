//	Copyright(c) 2020, Andy Do
//	eaglehorn58@gmail.com, eaglehorn58@163.com

using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ADTerrainStream
{
	public class TestTerrain : MonoBehaviour
	{
		public Camera cam = null;       //	bind camera to this
		[Range(0.5f, 1000f)] public float viewDistance = 200f;  //	current view distance

		private FileStream file = null;
		private int widthHM = 0;
		private int widthInGrids = 0;
		private int chunkGrid = 0;		//	chunk width in grids
		private float gridSize = 0;
		private int dataOffset = 0;
		private int chunkInRow = 0;

		private int[,] nodeCacheIndices = null;     //	chunk status buffer
		private List<List<TerrainChunk>> nodeCaches = null; //	chunk cache
		private int curCache = 0;

		private int nodeDataSize = 0;
		private byte[] dataBuffer = null;
		private float[] vertBuffer = null;

		private Material chunkMaterial = null;

		//	Loading thread contents
		private enum eLoadingEventsID
		{
			TO_LOAD = 0,    //	to load chunks
			TO_EXIT,        //	to exit thread
			HAS_EXITED,     //	loading thread has exited
			NUM_EVENT,
		};

		private Thread loadingThread = null;
		private AutoResetEvent[] autoLoadingEvents = null;
		private List<TerrainChunk> loadingRequestList = null;
		private List<TerrainChunk> loadingRequestTempList = null;
		private List<TerrainChunk> nodeLoadedList = null;
		private List<TerrainChunk> nodeLoadedTempList = null;

		// Start is called before the first frame update
		void Start()
		{
			//	try to open terrain data file
			string fileName = Application.dataPath + "/ADTerrainStream/Terrain.data";

			try
			{ 
				file = File.OpenRead(fileName);

				//	read file headers
				using (BinaryReader br = new BinaryReader(file, Encoding.UTF8, true))
				{
					widthHM = br.ReadInt32();
					widthInGrids = br.ReadInt32();
					chunkGrid = br.ReadInt32();
					gridSize = br.ReadSingle();
				}

				//	record start position for chunk data
				dataOffset = (int)file.Position;

				chunkInRow = (widthHM - 1) / chunkGrid;
				nodeCacheIndices = new int[chunkInRow, chunkInRow];

				for (int r = 0; r < chunkInRow; r++)
				{
					for (int c = 0; c < chunkInRow; c++)
					{
						nodeCacheIndices[r, c] = -1;
					}
				}

				//	create chunk caches, two caches are used in turns
				nodeCaches = new List<List<TerrainChunk>>();
				nodeCaches.Add(new List<TerrainChunk>());
				nodeCaches.Add(new List<TerrainChunk>());

				//	create temporary buffer used to load chunk data
				int vertNum = (chunkGrid + 1) * (chunkGrid + 1);
				nodeDataSize = vertNum * sizeof(float) * 3;
				dataBuffer = new byte[nodeDataSize];
				vertBuffer = new float[vertNum * 3];
			}
			catch
			{
				Debug.Log("TestTerrain.Start, failed!");
				return;
			}

			//	create stuffs for multithread loading
			loadingRequestList = new List<TerrainChunk>();
			loadingRequestTempList = new List<TerrainChunk>();
			nodeLoadedList = new List<TerrainChunk>();
			nodeLoadedTempList = new List<TerrainChunk>();

			autoLoadingEvents = new AutoResetEvent[(int)eLoadingEventsID.NUM_EVENT];
			autoLoadingEvents[(int)eLoadingEventsID.TO_LOAD] = new AutoResetEvent(false);
			autoLoadingEvents[(int)eLoadingEventsID.TO_EXIT] = new AutoResetEvent(false);
			autoLoadingEvents[(int)eLoadingEventsID.HAS_EXITED] = new AutoResetEvent(false);

			loadingThread = new Thread(this.LoadingThreadProc);
			loadingThread.Start();

			//	load material
			//chunkMaterial = new Material(Shader.Find("Standard"));
			chunkMaterial = Resources.Load<Material>("TerrainChunk");
		}

		private void OnDestroy()
		{
			if (loadingThread != null)
			{
				//	tell loading thread to exit and then wait it to do so.
				WaitHandle.SignalAndWait(autoLoadingEvents[(int)eLoadingEventsID.TO_EXIT],
					autoLoadingEvents[(int)eLoadingEventsID.HAS_EXITED]);

				//	in case there are some just loaded chunks
				foreach (TerrainChunk chunk in nodeLoadedList)
				{
					ReleaseNode(chunk);
				}
			}

			if (chunkMaterial)
			{
				Resources.UnloadAsset(chunkMaterial);
				Destroy(chunkMaterial);
				chunkMaterial = null;
			}

			if (file != null)
			{
				file.Dispose();
				file = null;
			}
		}

		// Update is called once per frame
		void Update()
		{
			if (cam == null || file == null)
				return;

			//	update visible chunk list
			UpdateTerrainNodes(cam.transform.position);

			//	generate unity objects for the nodes whose data are loaded
			GenerateNodeObjects();
		}

		//	update terrain chunks
		void UpdateTerrainNodes(Vector3 centerPos)
		{
			if (file == null)
				return;

			//	two cache take turns storing terrain chunks
			List<TerrainChunk> cache = nodeCaches[curCache];
			List<TerrainChunk> tempCache = nodeCaches[curCache ^ 1];

			float sx = -(widthInGrids / 2) * gridSize;
			float sz = -sx;
			float nodeSize = chunkGrid * gridSize;
			float halfNodeSize = nodeSize * 0.5f;

			int nodeInView = (int)(viewDistance / nodeSize) + 1;
			int loadFlag = 0;   //	0: remain; 1: load; 2: unload

			//	Lambda function used to move chunk between two caches
			Action<int, int> MoveNodeInCache = (int r, int c) =>
			{
				int index = nodeCacheIndices[r, c];
				int newIndex = tempCache.Count;
				tempCache.Add(cache[index]);
				nodeCacheIndices[r, c] = newIndex;
			};

			//	traverse all nodes to see which should be streamed in and which should be streamed out.
			for (int r = 0; r < chunkInRow; r++)
			{
				float nz = sz - nodeSize * r;
				float nx = sx;

				for (int c = 0; c < chunkInRow; c++)
				{
					loadFlag = 0;

					float deltaX = Mathf.Abs(centerPos.x - (nx + halfNodeSize));
					float deltaZ = Mathf.Abs(centerPos.z - (nz - halfNodeSize));

					//	check if chunk is in view distance. we give a larger checking distance for unloading
					//	terrain chunks than for loading them, this can prevent chunks from being loaded/unloaded 
					//	frequently when camera hovering around a point.
					if (deltaX - halfNodeSize < viewDistance && deltaZ - halfNodeSize < viewDistance)
					{
						//	chunk is in view distance
						loadFlag = 1;
					}
					else if (deltaX > viewDistance + halfNodeSize || deltaZ > viewDistance + halfNodeSize)
					{
						//	chunk is out of view distance
						loadFlag = 2;
					}

					if (loadFlag == 0)
					{
						//	remain current state. 
						//	if chunk has been created, just move it into the other cache
						if (nodeCacheIndices[r, c] >= 0)
						{
							MoveNodeInCache(r, c);
						}
					}
					else if (loadFlag == 1)
					{
						//	the chunk needs to be loaded
						if (nodeCacheIndices[r, c] >= 0)
						{
							//	chunk has been loaded, just move it into the other cache
							MoveNodeInCache(r, c);
						}
						else
						{
							//	create an empty chunk to hold a place in cache
							TerrainChunk chunk = new TerrainChunk(r, c);
							int newIndex = tempCache.Count;
							tempCache.Add(chunk);
							nodeCacheIndices[r, c] = newIndex;

							//	send out a loading request for the chunk
							AddLoadingRequest(chunk);
						}
					}
					else if (loadFlag == 2)
					{
						//	the chunk needs to be unloaded
						if (nodeCacheIndices[r, c] >= 0)
						{
							int index = nodeCacheIndices[r, c];
							TerrainChunk chunk = cache[index];

							if (chunk.inLoadingList)
							{
								//	the chunk has been put into loading list, so we just set its cancelLoading flag
								//	instead of destroying it by calling ReleaseNode() immediately, for loading thread 
								//	may be loading data for it right now. In this way, the chunk's destruction is postponed
								//	to GenerateNodeObjects() after its data has been loaded.
								chunk.cancelLoading = true;
							}
							else
							{
								//	this chunk has finished loading, so it can be released safely.
								ReleaseNode(chunk);
							}

							nodeCacheIndices[r, c] = -1;
						}
					}

					nx += nodeSize;
				}
			}

			//	Clear current cache
			cache.Clear();
			//	exchange cache
			curCache ^= 1;
		}

		//	add a loading request for specified chunk
		void AddLoadingRequest(TerrainChunk chunk)
		{
			chunk.inLoadingList = true;

			lock (loadingRequestList)
			{
#if DEBUG
				if (loadingRequestList.FindIndex(n => n == chunk) >= 0)
				{
					Debug.Log("TestTerrain.AddLoadingRequest, loading request already exist.");
				}
#endif
				loadingRequestList.Add(chunk);
				autoLoadingEvents[(int)eLoadingEventsID.TO_LOAD].Set();
			}
		}

		//	generate unity objects for loaded nodes
		void GenerateNodeObjects()
		{
			try
			{
				lock (nodeLoadedList)
				{
					nodeLoadedTempList.AddRange(nodeLoadedList);
					nodeLoadedList.Clear();
				}

				foreach (TerrainChunk chunk in nodeLoadedTempList)
				{
					if (chunk.cancelLoading)
					{
						//	Release chunk distinctly, so that unity objects can released.
						ReleaseNode(chunk);
					}
					else
					{
						//	generat unity objects
						chunk.GenerateObjects(chunkMaterial);
						//	loading process has finished, must reset inLoadingList flag
						chunk.inLoadingList = false;
					}
				}

				nodeLoadedTempList.Clear();
			}
			catch
			{
				nodeLoadedTempList.Clear();
			}
		}

		void LoadingThreadProc(object data)
		{
			while (true)
			{
				int index = WaitHandle.WaitAny(autoLoadingEvents, -1, false);
				if (index == (int)eLoadingEventsID.TO_LOAD)
				{
					HandleLoadingRequests_t();
				}
				else if (index == (int)eLoadingEventsID.TO_EXIT)
				{
					//	Tell main thread that I am going to exit
					autoLoadingEvents[(int)eLoadingEventsID.HAS_EXITED].Set();
					break;
				}
				else
				{
					//	this shouldn't happen
					Debug.Assert(false);
				}
			}

			Debug.Log("TestTerrain.LoadingThreadProc, loading thread exit");
		}

		//	Handle all node loading requests in loading thread
		void HandleLoadingRequests_t()
		{
			lock (loadingRequestList)
			{
				loadingRequestTempList.AddRange(loadingRequestList);
				loadingRequestList.Clear();
			}

			try
			{
				foreach (TerrainChunk node in loadingRequestTempList)
				{
					//	load node data from file
					StreamInNodeData_t(node);
				}

				lock (nodeLoadedList)
				{
					nodeLoadedList.AddRange(loadingRequestTempList);
				}

				loadingRequestTempList.Clear();
			}
			catch
			{
				loadingRequestTempList.Clear();
			}
		}

		//	Load terrain chunk data from file, this function is called from loading thread
		void StreamInNodeData_t(TerrainChunk chunk)
		{
			try
			{
				if (chunk.cancelLoading)
					return;

				int index = chunk.row * chunkInRow + chunk.col;
				int offset = dataOffset + nodeDataSize * index;

				file.Seek(offset, SeekOrigin.Begin);
				using (BinaryReader br = new BinaryReader(file, Encoding.UTF8, true))
				{
					br.Read(dataBuffer, 0, nodeDataSize);
					Buffer.BlockCopy(dataBuffer, 0, vertBuffer, 0, nodeDataSize);
				}

				int vertNum = (chunkGrid + 1) * (chunkGrid + 1);
				chunk.FillData(vertBuffer, vertNum, chunkGrid);
			}
			catch
			{
				Debug.Log("TestTerrain.CreateNode failed!");
			}
		}

		void ReleaseNode(TerrainChunk chunk)
		{
			if (chunk != null)
			{
				chunk.DestroyChunk();
			}
		}
	}
}

