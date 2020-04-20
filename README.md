# ADTerrainStream

Copyright(c) 2020, Andy Do.

License type: MIT

Contact: eaglehorn58@gmail.com, eaglehorn58@163.com

---------------------------------------------
The project was made by Unity2019.2.0f1 on Windows10.

A a simple example showing how to do streaming for custom terrain in Unity3D through .NET thread and file stream APIs. There isn't hierarchy or LOD mechanism in the simple custom terrain, but it is enough to explain the streaming process. 

The way to use the package:
1. Import the package into Unity3D.
2. Open the scene: Assets/ADTerrainStream/Scenes/TerrainStreamScene.unity
3. Select the menu item 'Tools->AD Terrain Stream' and click the 'Start' button in the popped window, this will create a 4K X 4K terrain and if succeed, a file will be generated at: Assets/ADTerrainStream/Terrain.data
4. Play the game.
5. Use WASDQE keys to move the camera and press right button to rotate it.
6. Adjust view distance on TestTerrain's inspector panel, adjust camera's speed on main camera's inspector panel.

See more info:
https://eaglehorn58.wixsite.com/homepage/post/an-example-of-streaming-custom-terrain-in-unity3d

