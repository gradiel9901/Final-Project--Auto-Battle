using UnityEngine;
using AI.Core;

namespace AI.States
{
    public class AttackState : BaseState
    {
        private float lastAttackTime;

        public AttackState(AIAgent agent, StateMachine stateMachine) : base(agent, stateMachine) { }

        public override void Enter()
        {
            // Stop moving — let local avoidance nudge slightly if crowded
            agent.navAgent.SetDestination(agent.transform.position);

            // Switch to idle immediately so the run animation doesn't linger
            if (agent.aiAnimator != null) agent.aiAnimator.PlayIdle();

            // Face target on arrival
            if (agent.currentTarget != null)
            {
                agent.transform.LookAt(agent.currentTarget.position);
            }
        }

        public override void Update()
        {
            if (!agent.IsTargetValid())
            {
                agent.currentTarget = null;
                stateMachine.ChangeState(new SearchState(agent, stateMachine));
                return;
            }

            float dist = Vector3.Distance(agent.transform.position, agent.currentTarget.position);
            // Must be slightly higher than ChaseState's entry threshold (which is 1.2f) to prevent state-looping
            if (dist > agent.attackRange * 1.25f) 
            {
                stateMachine.ChangeState(new ChaseState(agent, stateMachine));
                return;
            }

            // Keep facing target smoothly
            Vector3 direction = (agent.currentTarget.position - agent.transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                agent.transform.rotation = Quaternion.Slerp(agent.transform.rotation, lookRotation, Time.deltaTime * 5f);
            }

            // Once the attack swing finishes, return to idle stance
            if (agent.aiAnimator != null && agent.aiAnimator.HasFinishedAttack())
            {
                agent.aiAnimator.PlayIdle();
            }

            // Perform Attack
            agent.PerformAttack();
        }

        public override void Exit()
        {
        }
    }
}
