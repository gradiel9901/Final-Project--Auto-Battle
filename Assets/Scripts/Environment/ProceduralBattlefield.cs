using UnityEngine;
using Unity.AI.Navigation;
using System.Collections.Generic;

namespace Environment
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(NavMeshSurface))]
    public class ProceduralBattlefield : MonoBehaviour
    {
        [Header("Terrain Generation")]
        public bool deformMesh = true;
        [Tooltip("Default Unity Planes only have 10x10 vertices, which looks spiky. Check this to generate a dense custom mesh for smooth hills.")]
        public bool generateHighResPlane = true;
        public int resolution = 100; // 100x100 grid
        
        [Header("Terrain Deformation")]
        public float perlinScale = 0.5f;
        public float heightMultiplier = 5f;
        public float noiseOffsetX = 0f;
        public float noiseOffsetZ = 0f;

        [Header("Foliage & Obstacles")]
        public GameObject[] treePrefabs;
        public int treeCount = 50;
        
        public GameObject[] rockPrefabs;
        public int rockCount = 20;

        public GameObject[] shrubPrefabs;
        public int shrubCount = 100;

        [Header("Spawning Settings")]
        public float spawnPadding = 2f; // Keep away from the absolute edges
        public LayerMask terrainLayer;  // Used for raycasting down to find exactly where the deformed terrain is
        
        private MeshFilter meshFilter;
        private MeshCollider meshCollider;
        private NavMeshSurface navSurface;

        // Keep track of spawned objects so we can easily clear them
        [HideInInspector]
        public List<GameObject> spawnedProps = new List<GameObject>();

        [ContextMenu("1. Generate Full Level")]
        public void GenerateLevel()
        {
            InitializeComponents();

            // 1. Randomize Noise
            noiseOffsetX = Random.Range(0f, 9999f);
            noiseOffsetZ = Random.Range(0f, 9999f);

            // 2. Shape the Terrain (Plane)
            if (deformMesh)
            {
                DeformPlane();
            }

            // 3. Clear existing props
            ClearProps();

            // 4. Spawn new props
            SpawnPropGroup(treePrefabs, treeCount, "Trees");
            SpawnPropGroup(rockPrefabs, rockCount, "Rocks");
            SpawnPropGroup(shrubPrefabs, shrubCount, "Shrubs");

            // 5. Bake NavMesh
            BakeNavMesh();
            
            Debug.Log("Procedural Battlefield Generation Complete!");
        }

        [ContextMenu("2. Clear Props")]
        public void ClearProps()
        {
            // Walk backwards to safely destroy objects
            for (int i = spawnedProps.Count - 1; i >= 0; i--)
            {
                if (spawnedProps[i] != null)
                {
                    if (Application.isPlaying) Destroy(spawnedProps[i]);
                    else DestroyImmediate(spawnedProps[i]);
                }
            }
            spawnedProps.Clear();

            // Failsafe: also destroy any children that were manually added under our grouped folders
            foreach (Transform child in transform)
            {
                // We create container objects called "Trees Parent", "Rocks Parent", etc.
                if (child.name.Contains("Parent"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }

        private void InitializeComponents()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshCollider = GetComponent<MeshCollider>();
            navSurface = GetComponent<NavMeshSurface>();

            // Ensure the collider actually updates when we deform the mesh
            if (terrainLayer.value == 0) // If not set, default to default
            {
                terrainLayer = LayerMask.GetMask("Default"); 
            }
            
            // Unity planes need convex turned off for mesh colliders
            meshCollider.convex = false; 
        }

        private void DeformPlane()
        {
            Mesh targetMesh;
            
            if (generateHighResPlane)
            {
                targetMesh = GenerateFlatMesh(resolution);
            }
            else
            {
                // Create a copy so we don't permanently deform the Unity default Plane asset
                targetMesh = Instantiate(meshFilter.sharedMesh);
            }

            Vector3[] vertices = targetMesh.vertices;

            // Apply Perlin Noise
            for (int i = 0; i < vertices.Length; i++)
            {
                // Transform local vertex to world roughly for sampling, keeping it relative to object scale
                float xCoord = (vertices[i].x * transform.localScale.x * perlinScale) + noiseOffsetX;
                float zCoord = (vertices[i].z * transform.localScale.z * perlinScale) + noiseOffsetZ;

                // Mathf.PerlinNoise returns 0.0 to 1.0. Subtract 0.5 to allow valleys and hills.
                float yVal = (Mathf.PerlinNoise(xCoord, zCoord) - 0.5f) * heightMultiplier;
                
                // Unity planes are oriented flat, so Y in local space is up.
                vertices[i].y = yVal;
            }

            // Update Mesh
            targetMesh.vertices = vertices;
            targetMesh.RecalculateNormals();
            targetMesh.RecalculateBounds();
            
            meshFilter.mesh = targetMesh;
            
            // CRITICAL: Update the collider so raycasts for props actually hit the new hills!
            meshCollider.sharedMesh = targetMesh;
        }

        private Mesh GenerateFlatMesh(int res)
        {
            Mesh mesh = new Mesh();
            mesh.name = "ProceduralPlane";

            // Unity standard plane is 10x10 local units (-5 to +5)
            float size = 10f; 
            
            Vector3[] verts = new Vector3[(res + 1) * (res + 1)];
            Vector2[] uvs = new Vector2[verts.Length];
            int[] tris = new int[res * res * 6];

            for (int i = 0, z = 0; z <= res; z++)
            {
                for (int x = 0; x <= res; x++, i++)
                {
                    float xPos = ((float)x / res) * size - (size / 2f);
                    float zPos = ((float)z / res) * size - (size / 2f);
                    
                    verts[i] = new Vector3(xPos, 0, zPos);
                    uvs[i] = new Vector2((float)x / res, (float)z / res);
                }
            }

            for (int ti = 0, vi = 0, z = 0; z < res; z++, vi++)
            {
                for (int x = 0; x < res; x++, ti += 6, vi++)
                {
                    tris[ti] = vi;
                    tris[ti + 1] = vi + res + 1;
                    tris[ti + 2] = vi + 1;
                    tris[ti + 3] = vi + 1;
                    tris[ti + 4] = vi + res + 1;
                    tris[ti + 5] = vi + res + 2;
                }
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        private void SpawnPropGroup(GameObject[] prefabs, int count, string groupName)
        {
            if (prefabs == null || prefabs.Length == 0 || count <= 0) return;

            // Create a tidy folder for this group
            GameObject parentFolder = new GameObject($"{groupName} Parent");
            parentFolder.transform.SetParent(this.transform);
            
            // Get plane bounds in world space
            Bounds bounds = meshCollider.bounds;
            float minX = bounds.min.x + spawnPadding;
            float maxX = bounds.max.x - spawnPadding;
            float minZ = bounds.min.z + spawnPadding;
            float maxZ = bounds.max.z - spawnPadding;

            for (int i = 0; i < count; i++)
            {
                GameObject prefabToSpawn = prefabs[Random.Range(0, prefabs.Length)];
                if (prefabToSpawn == null) continue;

                // Pick a random X and Z within the plane
                Vector3 randomPos = new Vector3(Random.Range(minX, maxX), bounds.max.y + 50f, Random.Range(minZ, maxZ));

                // Raycast straight down from high above to find the exact Y height of the deformed terrain at this X/Z
                RaycastHit hit;
                if (Physics.Raycast(randomPos, Vector3.down, out hit, 1000f, terrainLayer))
                {
                    // Ensure we only hit THIS specific terrain object, not some random floating rock above it
                    if (hit.collider.gameObject == this.gameObject)
                    {
                        // Found the ground! Spawn it.
                        Vector3 finalPos = hit.point;
                        
                        // Random rotation around Y axis
                        Quaternion randomRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                        GameObject newProp = Instantiate(prefabToSpawn, finalPos, randomRot, parentFolder.transform);
                        
                        // Random scale variation (+/- 20%)
                        float randomScaleMult = Random.Range(0.8f, 1.2f);
                        newProp.transform.localScale *= randomScaleMult;

                        spawnedProps.Add(newProp);
                    }
                }
            }
        }

        private void BakeNavMesh()
        {
            if (navSurface != null)
            {
                // Ensure the surface knows it needs to collect objects dynamically so it sees our newly spawned trees
                navSurface.collectObjects = CollectObjects.All;
                navSurface.BuildNavMesh();
            }
        }
    }
}
