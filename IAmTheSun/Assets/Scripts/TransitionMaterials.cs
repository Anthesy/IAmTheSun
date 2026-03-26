using UnityEngine;
using DG.Tweening;

/// <summary>
/// Manages smooth material transitions between three planet visual states: Frozen, Alive, and Burnt.
/// Uses a single material slot with float/color blending during transitions.
/// </summary>
public class TransitionMaterials : MonoBehaviour
{
    private enum PlanetVisualState
    {
        Frozen,
        Alive,
        Burnt
    }

    private struct RuntimeBlendValues
    {
        public float transitionMainToSecondaryTexture;
        public float transitionWater;
        public Color planetBaseColor;
        public Color planetLayerColor;
        public Color planetSecondaryColor;
    }

    private struct RuntimeFresnelBlendValues
    {
        public float rimIntensity;
        public float atmosphereIntensity;
        public Color color;
        public float noiseSpeed;
        public float noiseScale;
        public float emissiveStrength;
    }

    private static readonly int TransitionMainToSecondaryTextureId = Shader.PropertyToID("_TransitionMainToSecondaryTexture");
    private static readonly int TransitionWaterId = Shader.PropertyToID("_TransitionWater");
    private static readonly int PlanetMaskId = Shader.PropertyToID("_PlanetMask");
    private static readonly int PlanetMaskGradiantId = Shader.PropertyToID("_PlanetMaskGradiant");
    private static readonly int PlanetBaseNormalId = Shader.PropertyToID("_PlanetBaseNormal");
    private static readonly int PlanetBaseTextureId = Shader.PropertyToID("_PlanetBaseTexture");
    private static readonly int PlanetLayerNormalId = Shader.PropertyToID("_PlanetLayerNormal");
    private static readonly int PlanetLayerTextureId = Shader.PropertyToID("_PlanetLayerTexture");
    private static readonly int PlanetSecondaryNormalId = Shader.PropertyToID("_PlanetSecondaryNormal");
    private static readonly int PlanetSecondaryTextureId = Shader.PropertyToID("_PlanetSecondaryTexture");
    private static readonly int PlanetBaseColorId = Shader.PropertyToID("_PlanetBaseColor");
    private static readonly int PlanetLayerColorId = Shader.PropertyToID("_PlanetLayerColor");
    private static readonly int PlanetSecondaryColorId = Shader.PropertyToID("_PlanetSecondaryColor");

    // Fresnel property IDs
    private static readonly int RimIntensityId = Shader.PropertyToID("_RimIntensity");
    private static readonly int AtmosphereIntensityId = Shader.PropertyToID("_AtmosphereIntensity");
    private static readonly int FresnelColorId = Shader.PropertyToID("_Color");
    private static readonly int NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");
    private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static readonly int EmissiveStrengthId = Shader.PropertyToID("_EmissiveStrength");

    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private int materialIndex = 0;
    [SerializeField] private int fresnelMaterialIndex = 1;
    [SerializeField] private float transitionDuration = 1.2f;
    [SerializeField] private Ease transitionEase = Ease.InOutSine;

    [Header("State Transition Delays")]
    [SerializeField] private float frozenToAliveDelay = 0f;
    [SerializeField] private float aliveToFrozenDelay = 0f;
    [SerializeField] private float aliveToBurntDelay = 5f;
    [SerializeField] private float burntToFrozenDelay = 0f;

    [Header("State Reference Materials")]
    [SerializeField] private Material frozenStateMaterial;
    [SerializeField] private Material aliveStateMaterial;
    [SerializeField] private Material burntStateMaterial;

    [Header("Fresnel State Reference Materials")]
    [SerializeField] private Material frozenFresnelStateMaterial;
    [SerializeField] private Material aliveFresnelStateMaterial;
    [SerializeField] private Material burntFresnelStateMaterial;

    private Material runtimeMaterial;
    private Material runtimeFresnelMaterial;
    private RuntimeBlendValues currentValues;
    private RuntimeFresnelBlendValues currentFresnelValues;
    private PlanetVisualState currentState;
    private bool isIlluminated;
    private bool isInitialized;
    private Tween activeTransitionTween;

    // Timers for tracking state transition delays
    private float frozenToAliveTimer;
    private float aliveToFrozenTimer;
    private float aliveToBurntTimer;
    private float burntToFrozenTimer;

    private void Reset()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        if (targetRenderer != null)
        {
            Material[] sharedMats = targetRenderer.sharedMaterials;
            if (sharedMats != null && sharedMats.Length > 0)
            {
                Material fallbackBase = (materialIndex >= 0 && materialIndex < sharedMats.Length) ? sharedMats[materialIndex] : sharedMats[0];
                Material fallbackFresnel = (fresnelMaterialIndex >= 0 && fresnelMaterialIndex < sharedMats.Length) ? sharedMats[fresnelMaterialIndex] : fallbackBase;

                if (frozenStateMaterial == null)
                    frozenStateMaterial = fallbackBase;

                if (aliveStateMaterial == null)
                    aliveStateMaterial = fallbackBase;

                if (burntStateMaterial == null)
                    burntStateMaterial = fallbackBase;

                if (frozenFresnelStateMaterial == null)
                    frozenFresnelStateMaterial = fallbackFresnel;

                if (aliveFresnelStateMaterial == null)
                    aliveFresnelStateMaterial = fallbackFresnel;

                if (burntFresnelStateMaterial == null)
                    burntFresnelStateMaterial = fallbackFresnel;
            }
        }
    }

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        if (targetRenderer == null)
        {
            Debug.LogError("TransitionMaterials: No Renderer found on this GameObject or children.", gameObject);
            return;
        }

        Material[] mats = targetRenderer.materials;

        if (mats.Length == 0)
        {
            Debug.LogError("TransitionMaterials: Renderer has no materials.", targetRenderer.gameObject);
            return;
        }

        if (materialIndex < 0 || materialIndex >= mats.Length)
        {
            Debug.LogError($"TransitionMaterials: Material index {materialIndex} out of range (count={mats.Length}).", targetRenderer.gameObject);
            return;
        }

        if (fresnelMaterialIndex < 0 || fresnelMaterialIndex >= mats.Length)
        {
            Debug.LogError($"TransitionMaterials: Fresnel material index {fresnelMaterialIndex} out of range (count={mats.Length}).", targetRenderer.gameObject);
            return;
        }

        // Create runtime instances
        if (mats[materialIndex] != null)
        {
            runtimeMaterial = new Material(mats[materialIndex]);
            mats[materialIndex] = runtimeMaterial;
        }

        if (mats[fresnelMaterialIndex] != null)
        {
            runtimeFresnelMaterial = new Material(mats[fresnelMaterialIndex]);
            mats[fresnelMaterialIndex] = runtimeFresnelMaterial;
        }

        targetRenderer.materials = mats;

        if (runtimeMaterial == null || runtimeFresnelMaterial == null)
        {
            Debug.LogError($"TransitionMaterials: Failed to create runtime materials.", targetRenderer.gameObject);
            return;
        }

        // Validate the frozen materials exist
        if (frozenStateMaterial == null || frozenFresnelStateMaterial == null)
        {
            Debug.LogError("TransitionMaterials: Frozen state materials are not assigned.", gameObject);
            return;
        }

        // Initialize to Frozen state
        Material initialStateMaterial = frozenStateMaterial;
        Material initialFresnelStateMaterial = frozenFresnelStateMaterial;
        
        RuntimeBlendValues initialValues = ToBlendValues(initialStateMaterial);
        RuntimeFresnelBlendValues initialFresnelValues = ToFresnelBlendValues(initialFresnelStateMaterial);
        
        currentValues = initialValues;
        currentFresnelValues = initialFresnelValues;
        
        ApplyTextures(initialStateMaterial, initialFresnelStateMaterial);
        ApplyBlendedValues();

        currentState = PlanetVisualState.Frozen;
        isIlluminated = false;
        frozenToAliveTimer = 0f;
        aliveToFrozenTimer = 0f;
        aliveToBurntTimer = 0f;
        burntToFrozenTimer = 0f;
        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        UpdateStateMachine();
    }

    public void SetIlluminated(bool illuminated)
    {
        isIlluminated = illuminated;
    }

    public void ApplyInside()
    {
        SetIlluminated(true);
    }

    public void ApplyOutside()
    {
        SetIlluminated(false);
    }

    private void UpdateStateMachine()
    {
        // Update transition timers based on current state and conditions
        if (currentState == PlanetVisualState.Frozen)
        {
            if (isIlluminated)
            {
                frozenToAliveTimer += Time.deltaTime;
            }
            else
            {
                frozenToAliveTimer = 0f;
            }
        }
        else
        {
            frozenToAliveTimer = 0f;
        }

        if (currentState == PlanetVisualState.Alive)
        {
            if (!isIlluminated)
            {
                aliveToFrozenTimer += Time.deltaTime;
            }
            else
            {
                aliveToFrozenTimer = 0f;
            }

            if (isIlluminated)
            {
                aliveToBurntTimer += Time.deltaTime;
            }
            else
            {
                aliveToBurntTimer = 0f;
            }
        }
        else
        {
            aliveToFrozenTimer = 0f;
            aliveToBurntTimer = 0f;
        }

        if (currentState == PlanetVisualState.Burnt)
        {
            if (!isIlluminated)
            {
                burntToFrozenTimer += Time.deltaTime;
            }
            else
            {
                burntToFrozenTimer = 0f;
            }
        }
        else
        {
            burntToFrozenTimer = 0f;
        }

        // Determine desired state
        PlanetVisualState desiredState = CalculateDesiredState();

        // If state should change
        if (desiredState != currentState)
        {
            StartTransitionToState(desiredState);
            currentState = desiredState;
            // Reset all timers on state change
            frozenToAliveTimer = 0f;
            aliveToFrozenTimer = 0f;
            aliveToBurntTimer = 0f;
            burntToFrozenTimer = 0f;
        }
    }

    private PlanetVisualState CalculateDesiredState()
    {
        switch (currentState)
        {
            case PlanetVisualState.Frozen:
                // Frozen → Alive when illuminated long enough
                return (isIlluminated && frozenToAliveTimer >= frozenToAliveDelay) ? PlanetVisualState.Alive : PlanetVisualState.Frozen;

            case PlanetVisualState.Alive:
                // Alive → Burnt when illuminated long enough
                if (isIlluminated && aliveToBurntTimer >= aliveToBurntDelay)
                    return PlanetVisualState.Burnt;

                // Alive → Frozen when not illuminated long enough
                if (!isIlluminated && aliveToFrozenTimer >= aliveToFrozenDelay)
                    return PlanetVisualState.Frozen;

                return PlanetVisualState.Alive;

            case PlanetVisualState.Burnt:
                // Burnt → Frozen when not illuminated long enough
                return (!isIlluminated && burntToFrozenTimer >= burntToFrozenDelay) ? PlanetVisualState.Frozen : PlanetVisualState.Burnt;

            default:
                return PlanetVisualState.Frozen;
        }
    }

    private Material GetStateReferenceMaterial(PlanetVisualState state)
    {
        switch (state)
        {
            case PlanetVisualState.Alive:
                return aliveStateMaterial;
            case PlanetVisualState.Burnt:
                return burntStateMaterial;
            default:
                return frozenStateMaterial;
        }
    }

    private Material GetFresnelStateReferenceMaterial(PlanetVisualState state)
    {
        switch (state)
        {
            case PlanetVisualState.Alive:
                return aliveFresnelStateMaterial;
            case PlanetVisualState.Burnt:
                return burntFresnelStateMaterial;
            default:
                return frozenFresnelStateMaterial;
        }
    }

    private RuntimeBlendValues ToBlendValues(Material source)
    {
        if (source == null)
        {
            return currentValues;
        }

        return new RuntimeBlendValues
        {
            transitionMainToSecondaryTexture = source.HasProperty(TransitionMainToSecondaryTextureId)
                ? source.GetFloat(TransitionMainToSecondaryTextureId)
                : 0f,
            transitionWater = source.HasProperty(TransitionWaterId)
                ? source.GetFloat(TransitionWaterId)
                : 0f,
            planetBaseColor = source.HasProperty(PlanetBaseColorId)
                ? source.GetColor(PlanetBaseColorId)
                : Color.white,
            planetLayerColor = source.HasProperty(PlanetLayerColorId)
                ? source.GetColor(PlanetLayerColorId)
                : Color.white,
            planetSecondaryColor = source.HasProperty(PlanetSecondaryColorId)
                ? source.GetColor(PlanetSecondaryColorId)
                : Color.white
        };
    }

    private RuntimeFresnelBlendValues ToFresnelBlendValues(Material source)
    {
        if (source == null)
        {
            return currentFresnelValues;
        }

        return new RuntimeFresnelBlendValues
        {
            rimIntensity = source.HasProperty(RimIntensityId)
                ? source.GetFloat(RimIntensityId)
                : 1f,
            atmosphereIntensity = source.HasProperty(AtmosphereIntensityId)
                ? source.GetFloat(AtmosphereIntensityId)
                : 1f,
            color = source.HasProperty(FresnelColorId)
                ? source.GetColor(FresnelColorId)
                : Color.white,
            noiseSpeed = source.HasProperty(NoiseSpeedId)
                ? source.GetFloat(NoiseSpeedId)
                : 1f,
            noiseScale = source.HasProperty(NoiseScaleId)
                ? source.GetFloat(NoiseScaleId)
                : 1f,
            emissiveStrength = source.HasProperty(EmissiveStrengthId)
                ? source.GetFloat(EmissiveStrengthId)
                : 1f
        };
    }

    private void ApplyTextures(Material baseMaterial, Material fresnelMaterial)
    {
        if (runtimeMaterial == null || baseMaterial == null)
        {
            return;
        }

        SetTextureFromMaterialIfExists(runtimeMaterial, PlanetMaskId, baseMaterial);
        SetTextureFromMaterialIfExists(runtimeMaterial, PlanetMaskGradiantId, baseMaterial);
        SetTextureFromMaterialIfExists(runtimeMaterial, PlanetBaseNormalId, baseMaterial);
        SetTextureFromMaterialIfExists(runtimeMaterial, PlanetBaseTextureId, baseMaterial);
        SetTextureFromMaterialIfExists(runtimeMaterial, PlanetLayerNormalId, baseMaterial);
        SetTextureFromMaterialIfExists(runtimeMaterial, PlanetLayerTextureId, baseMaterial);
        SetTextureFromMaterialIfExists(runtimeMaterial, PlanetSecondaryNormalId, baseMaterial);
        SetTextureFromMaterialIfExists(runtimeMaterial, PlanetSecondaryTextureId, baseMaterial);

        if (runtimeFresnelMaterial != null && fresnelMaterial != null)
        {
            SetTextureFromMaterialIfExists(runtimeFresnelMaterial, PlanetMaskId, fresnelMaterial);
            SetTextureFromMaterialIfExists(runtimeFresnelMaterial, PlanetMaskGradiantId, fresnelMaterial);
            SetTextureFromMaterialIfExists(runtimeFresnelMaterial, PlanetBaseNormalId, fresnelMaterial);
            SetTextureFromMaterialIfExists(runtimeFresnelMaterial, PlanetBaseTextureId, fresnelMaterial);
            SetTextureFromMaterialIfExists(runtimeFresnelMaterial, PlanetLayerNormalId, fresnelMaterial);
            SetTextureFromMaterialIfExists(runtimeFresnelMaterial, PlanetLayerTextureId, fresnelMaterial);
            SetTextureFromMaterialIfExists(runtimeFresnelMaterial, PlanetSecondaryNormalId, fresnelMaterial);
            SetTextureFromMaterialIfExists(runtimeFresnelMaterial, PlanetSecondaryTextureId, fresnelMaterial);
        }
    }

    private void StartTransitionToState(PlanetVisualState state)
    {
        Material targetMaterial = GetStateReferenceMaterial(state);
        Material targetFresnelMaterial = GetFresnelStateReferenceMaterial(state);
        
        if (targetMaterial == null || targetFresnelMaterial == null)
        {
            return;
        }

        ApplyTextures(targetMaterial, targetFresnelMaterial);
        
        // Base material transition
        RuntimeBlendValues fromValues = currentValues;
        RuntimeBlendValues toValues = ToBlendValues(targetMaterial);
        
        // Fresnel material transition
        RuntimeFresnelBlendValues fromFresnelValues = currentFresnelValues;
        RuntimeFresnelBlendValues toFresnelValues = ToFresnelBlendValues(targetFresnelMaterial);

        if (activeTransitionTween != null && activeTransitionTween.IsActive())
        {
            activeTransitionTween.Kill();
        }

        float tweenProgress = 0f;
        float safeDuration = Mathf.Max(0.01f, transitionDuration);

        activeTransitionTween = DOTween.To(() => tweenProgress, x =>
        {
            tweenProgress = x;
            
            // Interpolate base material
            currentValues.transitionMainToSecondaryTexture = Mathf.Lerp(fromValues.transitionMainToSecondaryTexture, toValues.transitionMainToSecondaryTexture, tweenProgress);
            currentValues.transitionWater = Mathf.Lerp(fromValues.transitionWater, toValues.transitionWater, tweenProgress);
            currentValues.planetBaseColor = Color.Lerp(fromValues.planetBaseColor, toValues.planetBaseColor, tweenProgress);
            currentValues.planetLayerColor = Color.Lerp(fromValues.planetLayerColor, toValues.planetLayerColor, tweenProgress);
            currentValues.planetSecondaryColor = Color.Lerp(fromValues.planetSecondaryColor, toValues.planetSecondaryColor, tweenProgress);
            
            // Interpolate fresnel material separately
            currentFresnelValues.rimIntensity = Mathf.Lerp(fromFresnelValues.rimIntensity, toFresnelValues.rimIntensity, tweenProgress);
            currentFresnelValues.atmosphereIntensity = Mathf.Lerp(fromFresnelValues.atmosphereIntensity, toFresnelValues.atmosphereIntensity, tweenProgress);
            currentFresnelValues.color = Color.Lerp(fromFresnelValues.color, toFresnelValues.color, tweenProgress);
            currentFresnelValues.noiseSpeed = Mathf.Lerp(fromFresnelValues.noiseSpeed, toFresnelValues.noiseSpeed, tweenProgress);
            currentFresnelValues.noiseScale = Mathf.Lerp(fromFresnelValues.noiseScale, toFresnelValues.noiseScale, tweenProgress);
            currentFresnelValues.emissiveStrength = Mathf.Lerp(fromFresnelValues.emissiveStrength, toFresnelValues.emissiveStrength, tweenProgress);
            
            ApplyBlendedValues();
        }, 1f, safeDuration)
        .SetEase(transitionEase)
        .SetLink(gameObject)
        .OnComplete(() =>
        {
            currentValues = toValues;
            currentFresnelValues = toFresnelValues;
            ApplyBlendedValues();
        });
    }

    private void ApplyBlendedValues()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        // Apply base material values
        if (runtimeMaterial.HasProperty(TransitionMainToSecondaryTextureId))
        {
            runtimeMaterial.SetFloat(TransitionMainToSecondaryTextureId, currentValues.transitionMainToSecondaryTexture);
        }

        if (runtimeMaterial.HasProperty(TransitionWaterId))
        {
            runtimeMaterial.SetFloat(TransitionWaterId, currentValues.transitionWater);
        }

        if (runtimeMaterial.HasProperty(PlanetBaseColorId))
        {
            runtimeMaterial.SetColor(PlanetBaseColorId, currentValues.planetBaseColor);
        }

        if (runtimeMaterial.HasProperty(PlanetLayerColorId))
        {
            runtimeMaterial.SetColor(PlanetLayerColorId, currentValues.planetLayerColor);
        }

        if (runtimeMaterial.HasProperty(PlanetSecondaryColorId))
        {
            runtimeMaterial.SetColor(PlanetSecondaryColorId, currentValues.planetSecondaryColor);
        }

        // Apply Fresnel material values (separately with fresnel-specific properties)
        if (runtimeFresnelMaterial != null)
        {
            if (runtimeFresnelMaterial.HasProperty(RimIntensityId))
            {
                runtimeFresnelMaterial.SetFloat(RimIntensityId, currentFresnelValues.rimIntensity);
            }

            if (runtimeFresnelMaterial.HasProperty(AtmosphereIntensityId))
            {
                runtimeFresnelMaterial.SetFloat(AtmosphereIntensityId, currentFresnelValues.atmosphereIntensity);
            }

            if (runtimeFresnelMaterial.HasProperty(FresnelColorId))
            {
                runtimeFresnelMaterial.SetColor(FresnelColorId, currentFresnelValues.color);
            }

            if (runtimeFresnelMaterial.HasProperty(NoiseSpeedId))
            {
                runtimeFresnelMaterial.SetFloat(NoiseSpeedId, currentFresnelValues.noiseSpeed);
            }

            if (runtimeFresnelMaterial.HasProperty(NoiseScaleId))
            {
                runtimeFresnelMaterial.SetFloat(NoiseScaleId, currentFresnelValues.noiseScale);
            }

            if (runtimeFresnelMaterial.HasProperty(EmissiveStrengthId))
            {
                runtimeFresnelMaterial.SetFloat(EmissiveStrengthId, currentFresnelValues.emissiveStrength);
            }
        }
    }

    private void SetTextureFromMaterialIfExists(Material destinationMaterial, int propertyId, Material source)
    {
        if (destinationMaterial.HasProperty(propertyId) && source.HasProperty(propertyId))
        {
            destinationMaterial.SetTexture(propertyId, source.GetTexture(propertyId));
        }
    }

    private void OnDestroy()
    {
        if (activeTransitionTween != null && activeTransitionTween.IsActive())
        {
            activeTransitionTween.Kill();
        }
    }
}
