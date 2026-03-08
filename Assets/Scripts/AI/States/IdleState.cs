using UnityEngine;
using AI.Core;

namespace AI.States
{
    public class IdleState : BaseState
    {
        private float wanderTimer;

        public IdleState(AIAgent agent, StateMachine stateMachine) : base(agent, stateMachine) { }

        public override void Enter()
        {
            agent.navAgent.isStopped = true;
            agent.navAgent.ResetPath();
            if (agent.aiAnimator != null) agent.aiAnimator.PlayIdle();
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

            // Patrol Logic
            if (agent.patrolPoints != null && agent.patrolPoints.Length > 0)
            {
                stateMachine.ChangeState(new PatrolState(agent, stateMachine));
            }
        }

        public override void Exit()
        {
            agent.navAgent.isStopped = false;
        }
    }
}
