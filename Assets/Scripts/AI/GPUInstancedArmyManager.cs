using System.Collections.Generic;
using UnityEngine;

namespace AI.GPU
{
    public class GPUInstancedArmyManager : MonoBehaviour
    {
        [Header("GPU Instancing Config")]
        public Mesh unitMesh;
        public Material instancedMaterial;
        
        // Unity allows up to 1023 instances per draw call for DrawMeshInstanced
        private const int MAX_INSTANCES_PER_BATCH = 1000;

        // The list of all active agents that we need to draw
        private List<AIAgent> activeAgents = new List<AIAgent>();

        // Buffers arrays
        private Matrix4x4[] matrices = new Matrix4x4[MAX_INSTANCES_PER_BATCH];
        private float[] animStateIndices = new float[MAX_INSTANCES_PER_BATCH];
        private float[] teamColors = new float[MAX_INSTANCES_PER_BATCH]; // 0 = Red, 1 = Green/Blue

        private MaterialPropertyBlock propertyBlock;

        public static GPUInstancedArmyManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            propertyBlock = new MaterialPropertyBlock();
        }

        public void RegisterAgent(AIAgent agent)
        {
            if (!activeAgents.Contains(agent))
            {
                activeAgents.Add(agent);
                
                // CRITICAL: Disable the standard SkinnedMeshRenderer + Animator so the CPU doesn't process them
                // We will render them ourselves via GPU.
                SkinnedMeshRenderer smr = agent.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null) smr.enabled = false;

                Animator anim = agent.GetComponentInChildren<Animator>();
                if (anim != null) anim.enabled = false;
            }
        }

        public void UnregisterAgent(AIAgent agent)
        {
            if (activeAgents.Contains(agent))
            {
                activeAgents.Remove(agent);
            }
        }

        private void Update()
        {
            if (unitMesh == null || instancedMaterial == null || activeAgents.Count == 0) return;

            int totalAgents = activeAgents.Count;
            int batches = Mathf.CeilToInt((float)totalAgents / MAX_INSTANCES_PER_BATCH);

            for (int b = 0; b < batches; b++)
            {
                int startIndex = b * MAX_INSTANCES_PER_BATCH;
                int count = Mathf.Min(MAX_INSTANCES_PER_BATCH, totalAgents - startIndex);

                for (int i = 0; i < count; i++)
                {
                    AIAgent agent = activeAgents[startIndex + i];
                    
                    // 1. Build TRS Matrix (Position, Rotation, Scale)
                    matrices[i] = Matrix4x4.TRS(agent.transform.position, agent.transform.rotation, agent.transform.localScale);

                    // 2. Pass State to Shader for Vertex Animation Texture (VAT) reading
                    // In a real VAT setup, we pass time offsets so animations aren't perfectly synced
                    float stateIndex = GetStateIndexForAgent(agent); 
                    animStateIndices[i] = stateIndex;

                    // 3. Pass Team Color
                    teamColors[i] = agent.team == Core.Team.Red ? 0f : 1f;
                }

                // Push custom data per-instance into the MaterialPropertyBlock
                propertyBlock.SetFloatArray("_AnimStateIndex", animStateIndices);
                propertyBlock.SetFloatArray("_TeamColorIndex", teamColors);

                // Issue the GPU Draw Call (1 call for up to 1,000 meshes)
                Graphics.DrawMeshInstanced(unitMesh, 0, instancedMaterial, matrices, count, propertyBlock);
            }
        }

        private float GetStateIndexForAgent(AIAgent agent)
        {
            if (agent.IsDead) return 3f; // 3 = Death Animation row in VAT
            
            // Very roughly mapping Unity state machine logic to a generic float index for the shader
            var state = agent.stateMachine?.CurrentState;
            
            if (state is States.AttackState) return 2f; // 2 = Attack
            if (state is States.ChaseState || state is States.SearchState || state is States.PatrolState) return 1f; // 1 = Run
            
            return 0f; // 0 = Idle
        }
    }
}
