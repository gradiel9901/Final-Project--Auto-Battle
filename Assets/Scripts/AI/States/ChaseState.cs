using UnityEngine;
using AI.Core;

namespace AI.States
{
    public class ChaseState : BaseState
    {
        public ChaseState(AIAgent agent, StateMachine stateMachine) : base(agent, stateMachine) { }

        public override void Enter()
        {
            agent.navAgent.isStopped = false;
            if (agent.aiAnimator != null) agent.aiAnimator.PlayRun();
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
            
            // Allow them to stop slightly earlier to form a line/circle around the target
            if (dist <= agent.attackRange * 1.2f)
            {
                // Let the NavMeshAgent decelerate naturally instead of hard stopping
                agent.navAgent.SetDestination(agent.transform.position); 
                stateMachine.ChangeState(new AttackState(agent, stateMachine));
                return;
            }

            agent.navAgent.SetDestination(agent.currentTarget.position);
        }

        public override void Exit()
        {
        }
    }
}
