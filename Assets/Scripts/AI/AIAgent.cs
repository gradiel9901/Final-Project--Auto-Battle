using UnityEngine;
using UnityEngine.AI;
using AI.Core;
using AI.States;
using AI.GPU;

namespace AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(StateMachine))]
    public class AIAgent : MonoBehaviour
    {
        [Header("Config")]
        public Team team;
        public UnitType unitType;
        public float maxHealth = 100f;
        public float damage = 10f;
        public float attackRange = 2f;
        public float attackRate = 3f; // Increased default since anim is long
        public float attackDelay = 2.433f; // Explicit delay for damage
        public float detectionRange = 10f;
        public Transform[] patrolPoints;

        [Header("Projectile for Caster")]
        public GameObject projectilePrefab;
        public Transform firePoint;

        [Header("Skills")]
        public bool hasTankSkill = false;
        public float tankSkillCooldown = 15f;
        public bool hasBruteSkill = false;
        public float bruteSkillCooldown = 10f;
        public bool hasSoldierSkill = false;

        [Header("Components")]
        public NavMeshAgent navAgent;
        public StateMachine stateMachine;
        public AI.Animations.AIAnimator aiAnimator;

        // Runtime
        [HideInInspector] public Transform currentTarget;
        private float lastAttackTime;
        [SerializeField] private float currentHealth;
        public bool IsDead { get; private set; }
        
        private bool waitingToDealDamage = false;
        private float damageDealTime;

        // Skill Tracking
        private bool isSkillActive = false;
        private float skillStartTime;
        private bool hasResurrected = false;
        
        private float lastTankSkillTime = -100f;
        private float lastBruteSkillTime = -100f;
        
        // Original Stats for Revert
        private float origMaxHealth;
        private float origAttackRate;
        private float origSpeed;
        private float origDamage;
        private Vector3 origScale;
        private float origAttackRange;
        
        private Coroutine scaleCoroutine;
        public float tankScaleDuration = 0.5f;

        private void OnValidate()
        {
            if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();
            if (stateMachine == null) stateMachine = GetComponent<StateMachine>();
            if (aiAnimator == null) aiAnimator = GetComponent<AI.Animations.AIAnimator>();
        }

        private void Start()
        {
            navAgent = GetComponent<NavMeshAgent>();
            stateMachine = GetComponent<StateMachine>();
            aiAnimator = GetComponent<AI.Animations.AIAnimator>();
            
            ConfigureStats();
            currentHealth = maxHealth;
            StoreOriginalStats();

            // Register to GPU Manager for Batched Rendering
            if (GPUInstancedArmyManager.Instance != null)
            {
                GPUInstancedArmyManager.Instance.RegisterAgent(this);
            }

            // --- NavMesh Avoidance & Spacing Setup ---
            navAgent.stoppingDistance = attackRange * 0.8f; // Stop just before attack range
            
            // Randomize priority so they don't all push each other equally (0 is most important, 99 is least)
            // Tanks are heavy, Casters are light
            if (unitType == UnitType.Tank) navAgent.avoidancePriority = Random.Range(20, 40);
            else if (unitType == UnitType.Brute) navAgent.avoidancePriority = Random.Range(40, 60);
            else if (unitType == UnitType.Soldier) navAgent.avoidancePriority = Random.Range(60, 80);
            else navAgent.avoidancePriority = Random.Range(80, 99); // Caster
            
            // Give them a slightly larger physical presence to prevent clipping meshes
            navAgent.radius = 0.5f;

            // Start in SearchState (Run Forward) instead of Idle
            stateMachine.Initialize(new AI.States.SearchState(this, stateMachine));
        }

        public bool IsTargetValid()
        {
            if (currentTarget == null) return false;
            
            AIAgent targetAgent = currentTarget.GetComponent<AIAgent>();
            if (targetAgent != null && targetAgent.IsDead) return false;

            return true;
        }

        private void ConfigureStats()
        {
            if (unitType == UnitType.Caster)
            {
                // Caster defaults if not set
                if (attackRange < 5f) attackRange = 10f;
                // Casters shouldn't wait 2.4s to spawn the projectile, their animation hand-raise is fast.
                // The projectile has its own travel time to delay damage.
                if (attackDelay > 1.0f) attackDelay = 0.6f;
            }
            
            // Ensure they can see enemies across the map if needed, 
            // otherwise they just wander aimlessly
            if (detectionRange < 50f) detectionRange = 50f; 
        }

        private void StoreOriginalStats()
        {
            origMaxHealth = maxHealth;
            origAttackRate = attackRate;
            if (navAgent != null) origSpeed = navAgent.speed;
            origDamage = damage;
            origScale = transform.localScale;
            origAttackRange = attackRange;
        }

        private void Update()
        {
            if (IsDead) return;

            // Sync Damage Logic (Timer Based)
            if (waitingToDealDamage)
            {
                if (Time.time >= damageDealTime)
                {
                    ApplyDamageToTarget();
                    waitingToDealDamage = false;
                }
            }

            // Skill Duration Monitor
            if (isSkillActive && Time.time >= skillStartTime + 5.0f)
            {
                RevertSkill();
            }

            // Debug visualization
            if (currentTarget != null)
            {
                Debug.DrawLine(transform.position, currentTarget.position, Color.red);
            }
            
            // Dynamic Target Re-evaluation (prevent chasing one guy forever if a closer one appears)
            if (Time.frameCount % 15 == 0 && currentTarget != null) // Check every 15 frames for performance
            {
                Transform closerEnemy = FindNearestEnemy();
                if (closerEnemy != null && closerEnemy != currentTarget)
                {
                    float distToCurrent = Vector3.Distance(transform.position, currentTarget.position);
                    float distToNew = Vector3.Distance(transform.position, closerEnemy.position);
                    
                    // If the new enemy is significantly closer, switch aggro
                    if (distToNew < distToCurrent - 2f) 
                    {
                        currentTarget = closerEnemy;
                    }
                }
            }
        }
        
        // ... (FindNearestEnemy, TakeDamage, Die match existing)

        public Transform FindNearestEnemy()
        {
            // Use FindObjectsByType to scan the entire map globally, ignoring detection ranges
            AIAgent[] allAgents = FindObjectsByType<AIAgent>(FindObjectsSortMode.None);
            Transform bestTarget = null;
            float closestDist = Mathf.Infinity;

            foreach (var other in allAgents)
            {
                if (other != null && other != this && other.team != this.team && !other.IsDead)
                {
                    float d = Vector3.Distance(transform.position, other.transform.position);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        bestTarget = other.transform;
                    }
                }
            }

            return bestTarget;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            // Trigger non-death skills on first incoming damage if un-activated and off cooldown
            if (!isSkillActive && !hasResurrected)
            {
                if (hasTankSkill && unitType == UnitType.Tank && Time.time >= lastTankSkillTime + tankSkillCooldown)
                {
                    ActivateTankSkill();
                }
                if (hasBruteSkill && unitType == UnitType.Brute && Time.time >= lastBruteSkillTime + bruteSkillCooldown)
                {
                    ActivateBruteSkill();
                }
            }

            currentHealth -= amount;
            Debug.Log($"{name} took {amount} damage. Current Health: {currentHealth}");

            if (currentHealth <= 0)
            {
                // Soldier Resurrection Logic check
                if (hasSoldierSkill && unitType == UnitType.Soldier && !hasResurrected)
                {
                    ActivateSoldierSkill();
                }
                else
                {
                    Die();
                }
            }
        }

        private void Die()
        {
            IsDead = true;
            if (aiAnimator != null) aiAnimator.PlayDeath();
            
            if (GPUInstancedArmyManager.Instance != null)
            {
                // Unregister from batched rendering (or keep if you want bodies to remain rendered)
                // GPUInstancedArmyManager.Instance.UnregisterAgent(this);
            }
            
            // Disable logic
            if (navAgent != null) navAgent.enabled = false;
            if (stateMachine != null) stateMachine.enabled = false;
            
            // Disable collider so others don't keep attacking or detecting
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            this.enabled = false;
        }

        public void PerformAttack()
        {
            if (waitingToDealDamage) return; 

            if (Time.time - lastAttackTime > attackRate)
            {
                lastAttackTime = Time.time;
                waitingToDealDamage = true;
                damageDealTime = Time.time + attackDelay; // Set timer
                
                if (aiAnimator != null) aiAnimator.PlayAttack();
            }
        }

        private void ApplyDamageToTarget()
        {
             // ... match existing ApplyDamageToTarget
            if (unitType == UnitType.Caster)
            {
                Debug.Log($"{name} (Caster) CASTS SPELL at {currentTarget.name}");
                if (projectilePrefab != null && firePoint != null)
                {
                    GameObject proj = Instantiate(projectilePrefab, firePoint.position, transform.rotation);
                    if (currentTarget != null)
                    {
                        Projectile projScript = proj.GetComponent<Projectile>();
                        if (projScript == null) projScript = proj.AddComponent<Projectile>();
                        
                        // Initialize with current target, damage, team, and a base travel speed
                        projScript.Initialize(currentTarget, damage, team, 15f);
                    }
                }
                else
                {
                    if (currentTarget != null)
                    {
                         AIAgent targetAgent = currentTarget.GetComponent<AIAgent>();
                         if (targetAgent != null) targetAgent.TakeDamage(damage);
                    }
                }
            }
            else
            {
                if (currentTarget != null)
                {
                    Debug.Log($"{name} ({unitType}) ATTACKS {currentTarget.name}");
                    AIAgent targetAgent = currentTarget.GetComponent<AIAgent>();
                    if (targetAgent != null)
                    {
                        targetAgent.TakeDamage(damage);
                    }
                }
            }
        }

        // --- SKILL LOGIC ---

        private void ActivateTankSkill()
        {
            isSkillActive = true;
            skillStartTime = Time.time;
            lastTankSkillTime = Time.time;
            
            // Smooth grow to 3x original scale
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(LerpScale(transform.localScale, origScale * 3f, tankScaleDuration));
            
            // Scale attack range and stopping distance with body size
            attackRange = origAttackRange * 3f;
            if (navAgent != null)
            {
                navAgent.stoppingDistance = attackRange * 0.8f;
            }
            
            // Max HP x 3 and heal to full
            maxHealth = origMaxHealth * 3f;
            currentHealth = maxHealth;
            
            Debug.Log($"{name} activated TANK SKILL: 3x Size, 3x HP!");
        }

        private void ActivateBruteSkill()
        {
            isSkillActive = true;
            skillStartTime = Time.time;
            lastBruteSkillTime = Time.time;
            
            // Immense attack and move speed
            attackRate = origAttackRate * 0.2f; // Attacks 5x faster
            if (navAgent != null) navAgent.speed = origSpeed * 3f; // Moves 3x faster
            
            Debug.Log($"{name} activated BRUTE SKILL: Immense Speed!");
        }

        private void ActivateSoldierSkill()
        {
            hasResurrected = true;
            
            // Resurrect
            currentHealth = origMaxHealth; // Or maybe half? Prompt said resurrected. We'll use 100%.
            
            // Increased attack and move speed (permanent until death again)
            attackRate = origAttackRate * 0.5f; 
            if (navAgent != null) navAgent.speed = origSpeed * 1.5f;
            
            Debug.Log($"{name} activated SOLDIER SKILL: Resurrected with bonus speed!");
        }

        private void RevertSkill()
        {
            isSkillActive = false;
            
            // Smooth shrink back to original scale
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(LerpScale(transform.localScale, origScale, tankScaleDuration));
            
            // Revert attack range and stopping distance
            attackRange = origAttackRange;
            if (navAgent != null)
            {
                navAgent.stoppingDistance = attackRange * 0.8f;
            }
            
            // Revert max health. If current health is now higher than the original max, clamp it down.
            maxHealth = origMaxHealth;
            if (currentHealth > maxHealth) currentHealth = maxHealth;
            
            // Revert speeds
            attackRate = origAttackRate;
            if (navAgent != null) navAgent.speed = origSpeed;
            
            Debug.Log($"{name}'s skill duration ended. Stats reverted.");
        }

        private System.Collections.IEnumerator LerpScale(Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.localScale = Vector3.Lerp(from, to, t);
                yield return null;
            }
            transform.localScale = to;
        }
    }
}
