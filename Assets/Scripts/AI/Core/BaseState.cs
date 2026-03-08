using UnityEngine;

namespace AI.Core
{
    public abstract class BaseState
    {
        protected AIAgent agent;
        protected StateMachine stateMachine;

        public BaseState(AIAgent agent, StateMachine stateMachine)
        {
            this.agent = agent;
            this.stateMachine = stateMachine;
        }

        public abstract void Enter();
        public abstract void Update();
        public abstract void Exit();
    }
}
