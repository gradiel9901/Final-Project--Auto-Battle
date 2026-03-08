using UnityEngine;

namespace AI.Animations
{
    [RequireComponent(typeof(Animator))]
    public class AIAnimator : MonoBehaviour
    {
        [Header("Animation State Names")]
        public string idleStateName = "Idle";
        public string runStateName = "Run";
        public string attackStateName = "Attack";
        public string deathStateName = "Death";

        private Animator animator;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        public void PlayIdle()
        {
            PlayAnimation(idleStateName);
        }

        public void PlayRun()
        {
            PlayAnimation(runStateName);
        }

        public void PlayAttack()
        {
            if (animator != null && !string.IsNullOrEmpty(attackStateName))
            {
                // Check current state to see if valid attack is already playing
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                
                // IsName check might vary depending on layer names, but usually matches the state name logic
                // If we are ALREADY playing the attack and it's not finished (normalizedTime < 1.0f), ignore the replay to prevent jitter.
                // (Unless the user WANTS to cancel into a new attack, but usually they want the swing to finish)
                if (stateInfo.IsName(attackStateName) && stateInfo.normalizedTime < 1.0f)
                {
                    return; 
                }

                animator.Play(attackStateName, -1, 0f);
            }
        }

        public bool HasFinishedAttack()
        {
            if (animator == null || string.IsNullOrEmpty(attackStateName)) return true;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            
            // Check if playing the attack state and time is close to 1 (end)
            // We use > 0.9f as a "completed" threshold
            if (stateInfo.IsName(attackStateName) && stateInfo.normalizedTime >= 0.9f)
            {
                return true;
            }
            return false;
        }

        public void PlayDeath()
        {
            PlayAnimation(deathStateName);
        }

        private void PlayAnimation(string stateName)
        {
            if (animator != null && !string.IsNullOrEmpty(stateName))
            {
                animator.CrossFade(stateName, 0.2f);
            }
        }
    }
}
