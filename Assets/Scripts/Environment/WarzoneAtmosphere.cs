using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Environment
{
    [RequireComponent(typeof(Volume))]
    public class WarzoneAtmosphere : MonoBehaviour
    {
        [Header("Lighting Settings")]
        [Tooltip("Assign your main directional light here.")]
        public Light directionalLight;
        public Color lightColor = new Color(0.89f, 0.45f, 0.15f); // Deep orange/amber
        public float lightIntensity = 0.8f;
        public float shadowStrength = 1.0f;
        
        [Tooltip("Assign an optional Skybox Material to apply (e.g., dark, cloudy, or reddish sky).")]
        public Material warzoneSkybox;

        [Header("Global Illumination (Ambient)")]
        public Color ambientSkyColor = new Color(0.15f, 0.1f, 0.05f); // Very dark brown/red
        public Color ambientEquatorColor = new Color(0.1f, 0.05f, 0.02f);
        public Color ambientGroundColor = new Color(0.02f, 0.01f, 0.01f);

        [Header("Fog Settings")]
        public bool enableFog = true;
        public Color fogColor = new Color(0.2f, 0.1f, 0.05f);
        public float fogDensity = 0.02f;

        [Header("Weather Settings")]
        [Tooltip("Assign a Rain Particle System to play when this volume is active.")]
        public ParticleSystem rainParticles;
        public bool enableLightning = true;
        public Color lightningColor = new Color(0.8f, 0.9f, 1f); // Cold white/blue flash
        public float minLightningInterval = 5f;
        public float maxLightningInterval = 15f;
        public float lightningPeakIntensity = 5f; // How bright the lightning is
        public AudioClip[] thunderSounds;
        public AudioSource weatherAudioSource;

        private Volume volume;
        private VolumeProfile profile;
        private Material originalSkybox;

        // URP Post Processing Components
        private ColorAdjustments colorAdjustments;
        private Bloom bloom;
        private Tonemapping tonemapping;
        private Vignette vignette;
        private WhiteBalance whiteBalance;
        private FilmGrain filmGrain;

        private Coroutine lightningCoroutine;

        private void OnEnable()
        {
            originalSkybox = RenderSettings.skybox;
            volume = GetComponent<Volume>();
            
            // Ensure we are working with an instanced profile so we don't overwrite project assets permanently
            if (volume.HasInstantiatedProfile())
            {
                profile = volume.profile;
            }
            else if (volume.sharedProfile != null)
            {
                volume.profile = Instantiate(volume.sharedProfile);
                profile = volume.profile;
            }
            else
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                volume.profile = profile;
            }

            SetupLightingAndFog();
            SetupPostProcessing();
            SetupProceduralRain();

            if (enableLightning && directionalLight != null)
            {
                lightningCoroutine = StartCoroutine(LightningRoutine());
            }
        }

        private void OnDisable()
        {
            if (lightningCoroutine != null)
            {
                StopCoroutine(lightningCoroutine);
                // Ensure light reverts
                if (directionalLight != null)
                {
                    directionalLight.intensity = lightIntensity;
                    directionalLight.color = lightColor;
                }
            }

            // Revert Skybox
            if (originalSkybox != null)
            {
                RenderSettings.skybox = originalSkybox;
            }

            if (rainParticles != null && rainParticles.isPlaying)
            {
                rainParticles.Stop();
            }
        }

        [ContextMenu("Apply Warzone Atmosphere")]
        public void ApplySettings()
        {
            SetupLightingAndFog();
            SetupPostProcessing();
            Debug.Log("Warzone Atmosphere Applied.");
        }

        private void SetupLightingAndFog()
        {
            // Apply Skybox
            if (warzoneSkybox != null)
            {
                RenderSettings.skybox = warzoneSkybox;
            }

            // Setup Directional Light
            if (directionalLight != null && directionalLight.type == LightType.Directional)
            {
                directionalLight.color = lightColor;
                directionalLight.intensity = lightIntensity;
                directionalLight.shadowStrength = shadowStrength;
            }

            // Setup Ambient Lighting (Gradient)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSkyColor;
            RenderSettings.ambientEquatorColor = ambientEquatorColor;
            RenderSettings.ambientGroundColor = ambientGroundColor;

            // Setup Fog
            RenderSettings.fog = enableFog;
            if (enableFog)
            {
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = fogDensity;
            }
        }

        private void SetupPostProcessing()
        {
            // 1. Color Adjustments: Darken, desaturate, increase contrast, tint towards rust/orange
            if (!profile.TryGet(out colorAdjustments))
            {
                colorAdjustments = profile.Add<ColorAdjustments>(true);
            }
            colorAdjustments.active = true;
            colorAdjustments.postExposure.Override(-0.5f);        // Slightly darker baseline
            colorAdjustments.contrast.Override(35f);              // High contrast for dramatic shadows
            colorAdjustments.colorFilter.Override(new Color(1f, 0.85f, 0.7f)); // Warm, dusty filter
            colorAdjustments.hueShift.Override(0f);
            colorAdjustments.saturation.Override(-20f);           // Desaturate to look bleak

            // 2. White Balance: Push temperature up for a hotter, fiery feel
            if (!profile.TryGet(out whiteBalance))
            {
                whiteBalance = profile.Add<WhiteBalance>(true);
            }
            whiteBalance.active = true;
            whiteBalance.temperature.Override(25f); // Warmer (orange)
            whiteBalance.tint.Override(10f);        // Slightly towards magenta/red to avoid pure green/yellow

            // 3. Bloom: Based on user screenshot target
            if (!profile.TryGet(out bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }
            bloom.active = true;
            bloom.highQualityFiltering.Override(true);
            bloom.threshold.Override(1.74f); 
            bloom.intensity.Override(0.36f); 
            bloom.scatter.Override(0.288f);  
            // Eyeballing the orange tint from the screenshot
            bloom.tint.Override(new Color(1.0f, 0.584f, 0.165f)); 

            // 4. Tonemapping: ACES gives a cinematic, highly contrasted curve
            if (!profile.TryGet(out tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
            }
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            // 5. Vignette: Darken the edges to claustrophobic/dramatic effect
            if (!profile.TryGet(out vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }
            vignette.active = true;
            vignette.color.Override(Color.black);
            vignette.intensity.Override(0.45f);
            vignette.smoothness.Override(0.4f);
            
            // 6. Film Grain: Gritty, dirty camera feel
            if (!profile.TryGet(out filmGrain))
            {
                filmGrain = profile.Add<FilmGrain>(true);
            }
            filmGrain.active = true;
            filmGrain.type.Override(FilmGrainLookup.Medium1);
            filmGrain.intensity.Override(0.6f);
            filmGrain.response.Override(0.8f);
        }

        private IEnumerator LightningRoutine()
        {
            while (enableLightning && directionalLight != null)
            {
                float waitTime = Random.Range(minLightningInterval, maxLightningInterval);
                yield return new WaitForSeconds(waitTime);

                // Play Audio
                if (weatherAudioSource != null && thunderSounds != null && thunderSounds.Length > 0)
                {
                    weatherAudioSource.PlayOneShot(thunderSounds[Random.Range(0, thunderSounds.Length)]);
                }

                // Flash sequence
                int flashes = Random.Range(1, 4); // 1 to 3 rapid flashes
                
                for (int i = 0; i < flashes; i++)
                {
                    // Lightning strikes (Cold white tint, extremely bright)
                    directionalLight.color = Color.Lerp(lightColor, lightningColor, 0.8f);
                    directionalLight.intensity = Random.Range(lightningPeakIntensity * 0.5f, lightningPeakIntensity);
                    
                    yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
                    
                    // Revert slightly inside the flash sequence
                    directionalLight.color = lightColor;
                    directionalLight.intensity = lightIntensity * 1.5f; 
                    
                    yield return new WaitForSeconds(Random.Range(0.02f, 0.1f));
                }

                // Fully revert back to the ambient warzone light
                directionalLight.color = lightColor;
                directionalLight.intensity = lightIntensity;
            }
        }

        private void SetupProceduralRain()
        {
            if (rainParticles != null) return; // Ignore if user assigned a custom prefab manually

            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            // Generate GameObject attached to the camera
            GameObject rainObj = new GameObject("WarzoneRain_Procedural");
            rainObj.transform.SetParent(mainCam.transform);
            rainObj.transform.localPosition = new Vector3(0, 15f, 5f); // High above and slightly forward of camera
            rainObj.transform.localRotation = Quaternion.Euler(90f, 0, 0); // Point down

            rainParticles = rainObj.AddComponent<ParticleSystem>();
            var renderer = rainObj.GetComponent<ParticleSystemRenderer>();
            
            // Assign Custom HLSL Material
            Shader rainShader = Shader.Find("VFX/CustomRainParticle");
            if (rainShader != null)
            {
                Material rainMat = new Material(rainShader);
                renderer.material = rainMat;
            }
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = 4.0f; // Stretch to look like rain
            renderer.velocityScale = 0.1f;

            // Shape Setup (Box emission high above camera)
            var shape = rainParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 40f, 1f); 

            // Main Settings
            var main = rainParticles.main;
            main.duration = 1.0f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(-30f, -40f); // Fall very fast downwards
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);  // Thin droplets
            main.simulationSpace = ParticleSystemSimulationSpace.World;   // Leave drops behind as camera moves
            main.maxParticles = 5000;
            main.playOnAwake = true;

            // Emission (Heavy rain)
            var emission = rainParticles.emission;
            emission.rateOverTime = 1500f;

            // Color tint (Dark, moody rain)
            var colorLife = rainParticles.colorOverLifetime;
            colorLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetAlphaKeys(new GradientAlphaKey[] {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(0.4f, 0.1f),
                new GradientAlphaKey(0.4f, 0.8f),
                new GradientAlphaKey(0.0f, 1.0f)
            });
            grad.SetColorKeys(new GradientColorKey[] {
                new GradientColorKey(new Color(0.8f, 0.9f, 1f), 0.0f),
                new GradientColorKey(new Color(0.8f, 0.9f, 1f), 1.0f)
            });
            colorLife.color = grad;

            if (Application.isPlaying) rainParticles.Play();
        }
    }
}
