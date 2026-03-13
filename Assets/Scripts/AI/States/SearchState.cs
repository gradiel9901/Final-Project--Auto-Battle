using UnityEngine;
using AI.Core;

namespace AI.States
{
    public class SearchState : BaseState
    {
        private Vector3 searchDestination;

        public SearchState(AIAgent agent, StateMachine stateMachine) : base(agent, stateMachine) { }

        public override void Enter()
        {
            agent.navAgent.isStopped = false;
            
            // Move strictly forward from where we are facing. 
            // Assuming units are spawned facing the enemy direction.
            // We set a far destination.
            searchDestination = agent.transform.position + agent.transform.forward * 100f;
            agent.navAgent.SetDestination(searchDestination);

            if (agent.aiAnimator != null) agent.aiAnimator.PlayRun();
        }

        public override void Update()
        {
            // Detection
            Transform enemies = agent.FindNearestEnemy();
            if (enemies != null)
            {
                agent.currentTarget = enemies;
                stateMachine.ChangeState(new ChaseState(agent, stateMachine));
                return;
            }

            // If we reached our current search point, pick a new random nearby point to wander
            if (!agent.navAgent.pathPending && agent.navAgent.remainingDistance < 1f)
            {
                // If there are literally no enemies left on the map, rest in IdleState
                AIAgent[] allAgents = Object.FindObjectsByType<AIAgent>(FindObjectsSortMode.None);
                bool enemiesExist = false;
                foreach (var other in allAgents)
                {
                    if (other != null && other != agent && other.team != agent.team && !other.IsDead)
                    {
                        enemiesExist = true;
                        break;
                    }
                }

                if (!enemiesExist)
                {
                    stateMachine.ChangeState(new IdleState(agent, stateMachine));
                    return;
                }

                // Generate a random point within a 15 unit radius to patrol/wander to
                Vector2 randomCircle = Random.insideUnitCircle * 15f;
                Vector3 randomDirection = new Vector3(randomCircle.x, 0, randomCircle.y);
                
                // Add to current agent position, not world zero
                Vector3 newPos = agent.transform.position + randomDirection;
                
                // Ensure the point is actually on the NavMesh
                UnityEngine.AI.NavMeshHit hit;
                if (UnityEngine.AI.NavMesh.SamplePosition(newPos, out hit, 15f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    searchDestination = hit.position;
                    agent.navAgent.SetDestination(searchDestination);
                }
            }
        }

        public override void Exit()
        {
        }
    }
}
