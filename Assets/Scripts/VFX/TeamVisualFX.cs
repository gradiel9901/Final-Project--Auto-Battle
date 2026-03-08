using UnityEngine;
using AI.Core;
using AI;

namespace VFX
{
    [RequireComponent(typeof(AIAgent))]
    public class TeamVisualFX : MonoBehaviour
    {
        public enum VisualElement { AutoDetect, Fire, Ice }
        
        [Header("Team Setup")]
        [Tooltip("AutoDetect will read the Team from AIAgent. (Assume Red = Fire, Green/Blue = Ice)")]
        public VisualElement elementOverride = VisualElement.AutoDetect;
        
        [Header("FX Config")]
        public float auraRadius = 1.0f;
        public float particleDensityMultiplier = 1.0f;

        private AIAgent agent;
        private ParticleSystem currentAura;

        private void Start()
        {
            agent = GetComponent<AIAgent>();
            GenerateAura();
        }

        [ContextMenu("Regenerate Aura")]
        public void GenerateAura()
        {
            if (currentAura != null)
            {
                if (Application.isPlaying) Destroy(currentAura.gameObject);
                else DestroyImmediate(currentAura.gameObject);
            }

            // Determine Element
            VisualElement activeElement = elementOverride;
            if (activeElement == VisualElement.AutoDetect)
            {
                if (agent != null && agent.team == Team.Red) activeElement = VisualElement.Fire;
                else activeElement = VisualElement.Ice; // Default green/blue to ice
            }

            // Create Object
            GameObject auraObj = new GameObject(activeElement.ToString() + "Aura_HQ");
            auraObj.transform.SetParent(this.transform);
            auraObj.transform.localPosition = Vector3.up * 1.0f; // Center of body
            auraObj.transform.localRotation = Quaternion.Euler(-90f, 0, 0); // Point upwards for standard Unity PS

            // Add Particle System
            currentAura = auraObj.AddComponent<ParticleSystem>();
            var renderer = auraObj.GetComponent<ParticleSystemRenderer>();

            // Setup Material based on HLSL Shader
            Material auraMat = null;
            if (activeElement == VisualElement.Fire)
            {
                Shader fireShader = Shader.Find("VFX/TeamFireParticle");
                if (fireShader != null) auraMat = new Material(fireShader);
                else Debug.LogWarning("Fire Shader not found! Using default particle material.");
            }
            else
            {
                Shader iceShader = Shader.Find("VFX/TeamIceParticle");
                if (iceShader != null) auraMat = new Material(iceShader);
                else Debug.LogWarning("Ice Shader not found! Using default particle material.");
            }
            
            if (auraMat != null) renderer.material = auraMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            
            // Force stop and clear before modifying structural parameters
            currentAura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = currentAura.main;
            main.duration = 5.0f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f); // Initial burst speed lowered
            main.startSize = new ParticleSystem.MinMaxCurve(1.0f, 1.8f); 
            main.startRotation = new ParticleSystem.MinMaxCurve(0, 360f * Mathf.Deg2Rad);
            main.gravityModifier = -0.3f; // Negative gravity makes them float up like heat/vapor
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World; 
            main.maxParticles = 100;

            var emission = currentAura.emission;
            emission.rateOverTime = 20f * particleDensityMultiplier;

            var shape = currentAura.shape;
            shape.shapeType = ParticleSystemShapeType.Cone; // Keep it as cone, but flat
            shape.radius = auraRadius * 0.4f; // Very tight base so it looks like it's coming from their feet/body
            shape.angle = 5f; // Straighter cone so physics handles the upward spread

            // Add constant upward draft
            var velocityOverLife = currentAura.velocityOverLifetime;
            velocityOverLife.enabled = true;
            // ALL axes must be the same curve mode type.
            velocityOverLife.x = new ParticleSystem.MinMaxCurve(0f);
            velocityOverLife.z = new ParticleSystem.MinMaxCurve(0f);
            velocityOverLife.y = new ParticleSystem.MinMaxCurve(1.5f, 3.5f); // Force pushes them upwards
            
            // Add slight noise to make the fire/steam "dance"
            var noiseOverLife = currentAura.noise;
            noiseOverLife.enabled = true;
            noiseOverLife.strength = 0.5f;
            noiseOverLife.frequency = 1.0f;
            noiseOverLife.scrollSpeed = 1.0f;

            var colorOverLife = currentAura.colorOverLifetime;
            colorOverLife.enabled = true;
            Gradient gradColor = new Gradient();
            
            // Standard visibility alpha, standard Alpha-Blending in the shader will prevent pure white blowout
            gradColor.SetAlphaKeys(new GradientAlphaKey[] { 
                new GradientAlphaKey(0.0f, 0.0f), 
                new GradientAlphaKey(0.6f, 0.2f), 
                new GradientAlphaKey(0.6f, 0.8f), 
                new GradientAlphaKey(0.0f, 1.0f) 
            });
            gradColor.SetColorKeys(new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 1f, 1f), 0.0f),
                new GradientColorKey(new Color(1f, 1f, 1f), 1.0f)
            });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(gradColor);

            var sizeOverLife = currentAura.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.5f);
            sizeCurve.AddKey(0.5f, 1.0f);
            sizeCurve.AddKey(1.0f, 0.1f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(2.0f, sizeCurve);

            var rotOverLife = currentAura.rotationOverLifetime;
            rotOverLife.enabled = true;
            rotOverLife.z = new ParticleSystem.MinMaxCurve(-1f, 1f); // Twirl gently

            // Play immediately
            if (Application.isPlaying) currentAura.Play();
        }
    }
}
