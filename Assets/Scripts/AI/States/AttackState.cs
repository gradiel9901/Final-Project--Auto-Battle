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
            // Instead of fully hard-stopping the agent, we just tell it its destination is itself.
            // This allows the local avoidance system to still push the unit slightly if overcrowded.
            agent.navAgent.SetDestination(agent.transform.position);
            
            // Ensure we aren't rubbing against them. 
            // Note: NavMeshAgent might slide a bit, but this helps logic.
            
            // Look at target
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
            if (dist > agent.attackRange + 0.5f) // Buffer to prevent jitter
            {
                stateMachine.ChangeState(new ChaseState(agent, stateMachine));
                return;
            }

            // Keep looking at target
            Vector3 direction = (agent.currentTarget.position - agent.transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                agent.transform.rotation = Quaternion.Slerp(agent.transform.rotation, lookRotation, Time.deltaTime * 5f);
            }

            // Perform Attack
            agent.PerformAttack();
        }

        public override void Exit()
        {
        }
    }
}
