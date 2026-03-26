using UnityEngine;
using DG.Tweening;

public class TransitionMaterials : MonoBehaviour
{
    private enum PlanetVisualState
    {
        Frozen,
        Alive,
        Burnt
    }

    [System.Serializable]
    private struct StateTransitionDelay
    {
        public PlanetVisualState from;
        public PlanetVisualState to;
        [Min(0f)] public float delay;
    }

    private struct RuntimeBlendValues
    {
        public float transitionMainToSecondaryTexture;
        public float transitionWater;
        public Color planetBaseColor;
        public Color planetLayerColor;
        public Color planetSecondaryColor;
    }

    private static readonly int TransitionMainToSecondaryTextureId = Shader.PropertyToID("_TransitionMainToSecondaryTexture");
    private static readonly int TransitionWaterId = Shader.PropertyToID("_TransitionWater");
    private static readonly int PlanetMaskId = Shader.PropertyToID("_PlanetMask");
    private static readonly int PlanetMaskGradiantId = Shader.PropertyToID("_PlanetMaskGradiant");
    private static readonly int PlanetBaseNormalId = Shader.PropertyToID("_PlanetBaseNormal");
    private static readonly int PlanetBaseTextureId = Shader.PropertyToID("_PlanetBaseTexture");
    private static readonly int PlanetBaseColorId = Shader.PropertyToID("_PlanetBaseColor");
    private static readonly int PlanetLayerNormalId = Shader.PropertyToID("_PlanetLayerNormal");
    private static readonly int PlanetLayerTextureId = Shader.PropertyToID("_PlanetLayerTexture");
    private static readonly int PlanetLayerColorId = Shader.PropertyToID("_PlanetLayerColor");
    private static readonly int PlanetSecondaryNormalId = Shader.PropertyToID("_PlanetSecondaryNormal");
    private static readonly int PlanetSecondaryTextureId = Shader.PropertyToID("_PlanetSecondaryTexture");
    private static readonly int PlanetSecondaryColorId = Shader.PropertyToID("_PlanetSecondaryColor");

    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private int baseMaterialIndex = 0;
    [SerializeField] private int fresnelMaterialIndex = 1;
    [SerializeField] private float transitionDuration = 1.2f;
    [SerializeField] private Ease transitionEase = Ease.InOutSine;

    [Header("Base State Reference Materials")]
    [SerializeField] private Material aliveBaseStateMaterial;
    [SerializeField] private Material burntBaseStateMaterial;
    [SerializeField] private Material frozenBaseStateMaterial;

    [Header("Fresnel State Reference Materials")]
    
    [SerializeField] private Material aliveFresnelStateMaterial;
    [SerializeField] private Material burntFresnelStateMaterial;
    [SerializeField] private Material frozenFresnelStateMaterial;

    [Header("Per Transition Delay")]
    [SerializeField] private StateTransitionDelay[] transitionDelays =
    {
        new StateTransitionDelay { from = PlanetVisualState.Frozen, to = PlanetVisualState.Alive, delay = 0.2f },
        new StateTransitionDelay { from = PlanetVisualState.Alive, to = PlanetVisualState.Frozen, delay = 0.2f },
        new StateTransitionDelay { from = PlanetVisualState.Alive, to = PlanetVisualState.Burnt, delay = 5f },
        new StateTransitionDelay { from = PlanetVisualState.Burnt, to = PlanetVisualState.Alive, delay = 0.5f },
        new StateTransitionDelay { from = PlanetVisualState.Frozen, to = PlanetVisualState.Burnt, delay = 6f },
        new StateTransitionDelay { from = PlanetVisualState.Burnt, to = PlanetVisualState.Frozen, delay = 0.5f }
    };

    private Material runtimeBaseMaterial;
    private Material runtimeFresnelMaterial;
    private RuntimeBlendValues currentValues;
    private PlanetVisualState currentState = PlanetVisualState.Frozen;
    private PlanetVisualState pendingState = PlanetVisualState.Frozen;
    private bool isIlluminated;
    private float stateChangeTimer;
    private bool isInitialized;
    private Tween activeTransitionTween;

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
                Material fallbackBase = (baseMaterialIndex >= 0 && baseMaterialIndex < sharedMats.Length) ? sharedMats[baseMaterialIndex] : sharedMats[0];
                Material fallbackFresnel = (fresnelMaterialIndex >= 0 && fresnelMaterialIndex < sharedMats.Length) ? sharedMats[fresnelMaterialIndex] : fallbackBase;

                if (frozenBaseStateMaterial == null)
                {
                    frozenBaseStateMaterial = fallbackBase;
                }

                if (aliveBaseStateMaterial == null)
                {
                    aliveBaseStateMaterial = fallbackBase;
                }

                if (burntBaseStateMaterial == null)
                {
                    burntBaseStateMaterial = fallbackBase;
                }

                if (frozenFresnelStateMaterial == null)
                {
                    frozenFresnelStateMaterial = fallbackFresnel;
                }

                if (aliveFresnelStateMaterial == null)
                {
                    aliveFresnelStateMaterial = fallbackFresnel;
                }

                if (burntFresnelStateMaterial == null)
                {
                    burntFresnelStateMaterial = fallbackFresnel;
                }
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
            Debug.LogWarning("TransitionMaterials: No Renderer found.");
            return;
        }

        Material[] mats = targetRenderer.materials;

        if (mats.Length == 0)
        {
            Debug.LogWarning("TransitionMaterials: Renderer has no materials.");
            return;
        }

        if (baseMaterialIndex >= 0 && baseMaterialIndex < mats.Length)
        {
            runtimeBaseMaterial = mats[baseMaterialIndex];
        }

        if (fresnelMaterialIndex >= 0 && fresnelMaterialIndex < mats.Length)
        {
            runtimeFresnelMaterial = mats[fresnelMaterialIndex];
        }

        if (runtimeBaseMaterial == null || runtimeFresnelMaterial == null)
        {
            Debug.LogWarning($"TransitionMaterials: invalid material indices on renderer '{targetRenderer.name}' (count={mats.Length}), baseIndex={baseMaterialIndex}, fresnelIndex={fresnelMaterialIndex}.");
            return;
        }

        if (!runtimeBaseMaterial.HasProperty(TransitionWaterId))
        {
            Debug.LogWarning($"TransitionMaterials: '_TransitionWater' not found on base material index {baseMaterialIndex} ('{runtimeBaseMaterial.name}').");
            return;
        }

        Material initialBaseStateMaterial = GetBaseStateReferenceMaterial(PlanetVisualState.Frozen);
        Material initialFresnelStateMaterial = GetFresnelStateReferenceMaterial(PlanetVisualState.Frozen);
        RuntimeBlendValues initialValues = ToBlendValues(initialBaseStateMaterial);
        currentValues = initialValues;
        ApplyTextures(runtimeBaseMaterial, initialBaseStateMaterial);
        ApplyTextures(runtimeFresnelMaterial, initialFresnelStateMaterial);
        ApplyBlendedValues();

        currentState = PlanetVisualState.Frozen;
        pendingState = currentState;
        isIlluminated = false;
        stateChangeTimer = 0f;
        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        UpdateStateMachine();
        // Values are blended by DOTween when target state changes.
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
        PlanetVisualState desiredState = isIlluminated ? PlanetVisualState.Alive : PlanetVisualState.Frozen;

        if (pendingState != desiredState)
        {
            pendingState = desiredState;
            stateChangeTimer = 0f;
        }

        if (pendingState == currentState)
        {
            return;
        }

        float requiredDelay = GetTransitionDelay(currentState, pendingState);
        stateChangeTimer += Time.deltaTime;

        if (stateChangeTimer < requiredDelay)
        {
            return;
        }

        StartTransitionToState(pendingState);
        currentState = pendingState;
        stateChangeTimer = 0f;
    }

    private float GetTransitionDelay(PlanetVisualState from, PlanetVisualState to)
    {
        if (transitionDelays == null)
        {
            return 0f;
        }

        for (int i = 0; i < transitionDelays.Length; i++)
        {
            if (transitionDelays[i].from == from && transitionDelays[i].to == to)
            {
                return Mathf.Max(0f, transitionDelays[i].delay);
            }
        }

        return 0f;
    }

    private Material GetBaseStateReferenceMaterial(PlanetVisualState state)
    {
        switch (state)
        {
            case PlanetVisualState.Alive:
                return aliveBaseStateMaterial;
            case PlanetVisualState.Burnt:
                return burntBaseStateMaterial;
            default:
                return frozenBaseStateMaterial;
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

    private void ApplyTextures(Material destinationMaterial, Material stateMaterial)
    {
        if (destinationMaterial == null || stateMaterial == null)
        {
            return;
        }

        SetTextureFromMaterialIfExists(destinationMaterial, PlanetMaskId, stateMaterial);
        SetTextureFromMaterialIfExists(destinationMaterial, PlanetMaskGradiantId, stateMaterial);
        SetTextureFromMaterialIfExists(destinationMaterial, PlanetBaseNormalId, stateMaterial);
        SetTextureFromMaterialIfExists(destinationMaterial, PlanetBaseTextureId, stateMaterial);
        SetTextureFromMaterialIfExists(destinationMaterial, PlanetLayerNormalId, stateMaterial);
        SetTextureFromMaterialIfExists(destinationMaterial, PlanetLayerTextureId, stateMaterial);
        SetTextureFromMaterialIfExists(destinationMaterial, PlanetSecondaryNormalId, stateMaterial);
        SetTextureFromMaterialIfExists(destinationMaterial, PlanetSecondaryTextureId, stateMaterial);
    }

    private void StartTransitionToState(PlanetVisualState state)
    {
        Material targetBaseMaterial = GetBaseStateReferenceMaterial(state);
        Material targetFresnelMaterial = GetFresnelStateReferenceMaterial(state);

        if (targetBaseMaterial == null || targetFresnelMaterial == null)
        {
            return;
        }

        ApplyTextures(runtimeBaseMaterial, targetBaseMaterial);
        ApplyTextures(runtimeFresnelMaterial, targetFresnelMaterial);
        RuntimeBlendValues fromValues = currentValues;
        RuntimeBlendValues toValues = ToBlendValues(targetBaseMaterial);

        if (activeTransitionTween != null && activeTransitionTween.IsActive())
        {
            activeTransitionTween.Kill();
        }

        float tweenProgress = 0f;
        float safeDuration = Mathf.Max(0.01f, transitionDuration);

        activeTransitionTween = DOTween.To(() => tweenProgress, x =>
        {
            tweenProgress = x;
            currentValues.transitionMainToSecondaryTexture = Mathf.Lerp(fromValues.transitionMainToSecondaryTexture, toValues.transitionMainToSecondaryTexture, tweenProgress);
            currentValues.transitionWater = Mathf.Lerp(fromValues.transitionWater, toValues.transitionWater, tweenProgress);
            currentValues.planetBaseColor = Color.Lerp(fromValues.planetBaseColor, toValues.planetBaseColor, tweenProgress);
            currentValues.planetLayerColor = Color.Lerp(fromValues.planetLayerColor, toValues.planetLayerColor, tweenProgress);
            currentValues.planetSecondaryColor = Color.Lerp(fromValues.planetSecondaryColor, toValues.planetSecondaryColor, tweenProgress);
            ApplyBlendedValues();
        }, 1f, safeDuration)
        .SetEase(transitionEase)
        .SetLink(gameObject)
        .OnComplete(() =>
        {
            currentValues = toValues;
            ApplyBlendedValues();
        });
    }

    private void ApplyBlendedValues()
    {
        if (runtimeBaseMaterial == null)
        {
            return;
        }

        SetFloatIfExists(TransitionMainToSecondaryTextureId, currentValues.transitionMainToSecondaryTexture);
        SetFloatIfExists(TransitionWaterId, currentValues.transitionWater);
        SetColorIfExists(PlanetBaseColorId, currentValues.planetBaseColor);
        SetColorIfExists(PlanetLayerColorId, currentValues.planetLayerColor);
        SetColorIfExists(PlanetSecondaryColorId, currentValues.planetSecondaryColor);
    }

    private void SetFloatIfExists(int propertyId, float value)
    {
        if (runtimeBaseMaterial.HasProperty(propertyId))
        {
            runtimeBaseMaterial.SetFloat(propertyId, value);
        }
    }

    private void SetColorIfExists(int propertyId, Color value)
    {
        if (runtimeBaseMaterial.HasProperty(propertyId))
        {
            runtimeBaseMaterial.SetColor(propertyId, value);
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
