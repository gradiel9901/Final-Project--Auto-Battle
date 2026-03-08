using UnityEngine;

namespace AI.Core
{
    public class Projectile : MonoBehaviour
    {
        private Transform target;
        private float damage;
        private Team sourceTeam;
        private float speed;
        private bool initialized = false;

        public void Initialize(Transform targetTransform, float dmg, Team team, float moveSpeed)
        {
            target = targetTransform;
            damage = dmg;
            sourceTeam = team;
            speed = moveSpeed;
            initialized = true;

            // Failsafe destroy after a few seconds in case it misses or target goes missing forever
            Destroy(gameObject, 10f);
        }

        private void Update()
        {
            if (!initialized) return;

            if (target == null)
            {
                // Target died or vanished
                Destroy(gameObject);
                return;
            }

            // Aim slightly above the pivot so it hits the chest instead of their feet.
            Vector3 targetPos = target.position + Vector3.up * 1.25f;
            Vector3 direction = (targetPos - transform.position).normalized;
            
            transform.position += direction * speed * Time.deltaTime;
            
            // Adjust rotation to face target
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 720f * Time.deltaTime);
            }

            // Collision/Hit check by distance
            if (Vector3.Distance(transform.position, targetPos) <= 0.6f)
            {
                HitTarget();
            }
        }

        private void HitTarget()
        {
            if (target != null)
            {
                AIAgent agent = target.GetComponent<AIAgent>();
                // Guarantee we don't heal them or something, but realistically we only set enemies as target.
                if (agent != null && agent.team != sourceTeam && !agent.IsDead)
                {
                    agent.TakeDamage(damage);
                }
            }
            
            // Wrangle up children particle systems and detach them so they don't immediately pop out of existence
            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>();
            foreach(ParticleSystem ps in particles)
            {
                ps.transform.SetParent(null);
                ps.Stop();
                Destroy(ps.gameObject, 3f);
            }

            Destroy(gameObject);
        }
    }
}
