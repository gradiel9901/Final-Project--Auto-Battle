using UnityEngine;
using AI.Core;

namespace AI.States
{
    public class PatrolState : BaseState
    {
        private int patrolIndex = 0;

        public PatrolState(AIAgent agent, StateMachine stateMachine) : base(agent, stateMachine) { }

        public override void Enter()
        {
            agent.navAgent.isStopped = false;
            if (agent.aiAnimator != null) agent.aiAnimator.PlayRun();
            if (agent.patrolPoints != null && agent.patrolPoints.Length > 0)
            {
                SetDestination();
            }
            else
            {
                stateMachine.ChangeState(new SearchState(agent, stateMachine));
            }
        }

        public override void Update()
        {
            // Detection
            Transform enemy = agent.FindNearestEnemy();
            if (enemy != null)
            {
                agent.currentTarget = enemy;
                stateMachine.ChangeState(new ChaseState(agent, stateMachine));
                return;
            }

            // Patrol Logic
            if (!agent.navAgent.pathPending && agent.navAgent.remainingDistance < 0.5f)
            {
                patrolIndex = (patrolIndex + 1) % agent.patrolPoints.Length;
                SetDestination();
            }
        }

        private void SetDestination()
        {
            if (agent.patrolPoints.Length > 0)
            {
                agent.navAgent.SetDestination(agent.patrolPoints[patrolIndex].position);
            }
        }

        public override void Exit()
        {
        }
    }
}
