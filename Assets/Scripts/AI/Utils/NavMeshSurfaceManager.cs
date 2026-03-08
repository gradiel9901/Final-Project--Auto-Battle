using UnityEngine;
using Unity.AI.Navigation;

namespace AI.Utils
{
    [RequireComponent(typeof(NavMeshSurface))]
    public class NavMeshSurfaceManager : MonoBehaviour
    {
        public NavMeshSurface surface;

        private void Awake()
        {
            surface = GetComponent<NavMeshSurface>();
            // Optional: Bake on awake if dynamic
            // surface.BuildNavMesh();
        }

        [ContextMenu("Bake NavMesh")]
        public void BakeNavMesh()
        {
            if (surface != null)
            {
                surface.BuildNavMesh();
                Debug.Log("NavMesh Baked");
            }
        }
    }
}
