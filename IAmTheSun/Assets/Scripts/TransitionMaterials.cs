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

    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private int materialIndex = 0;
    [SerializeField] private float transitionDuration = 1.2f;
    [SerializeField] private Ease transitionEase = Ease.InOutSine;
    [SerializeField] private float burnDelay = 5f;

    [Header("State Reference Materials")]
    [SerializeField] private Material frozenStateMaterial;
    [SerializeField] private Material aliveStateMaterial;
    [SerializeField] private Material burntStateMaterial;

    private Material runtimeMaterial;
    private RuntimeBlendValues currentValues;
    private PlanetVisualState currentState;
    private bool isIlluminated;
    private float illuminationTimer;
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
                Material fallback = (materialIndex >= 0 && materialIndex < sharedMats.Length) ? sharedMats[materialIndex] : sharedMats[0];

                if (frozenStateMaterial == null)
                {
                    frozenStateMaterial = fallback;
                }

                if (aliveStateMaterial == null)
                {
                    aliveStateMaterial = fallback;
                }

                if (burntStateMaterial == null)
                {
                    burntStateMaterial = fallback;
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

        // Create runtime instance from the material at the specified index
        if (mats[materialIndex] != null)
        {
            runtimeMaterial = new Material(mats[materialIndex]);
            mats[materialIndex] = runtimeMaterial;
            targetRenderer.materials = mats;
        }

        if (runtimeMaterial == null)
        {
            Debug.LogError($"TransitionMaterials: Failed to create runtime material at index {materialIndex}.", targetRenderer.gameObject);
            return;
        }

        // Validate the frozen material exists
        if (frozenStateMaterial == null)
        {
            Debug.LogError("TransitionMaterials: Frozen state material is not assigned.", gameObject);
            return;
        }

        // Initialize to Frozen state
        Material initialStateMaterial = frozenStateMaterial;
        RuntimeBlendValues initialValues = ToBlendValues(initialStateMaterial);
        currentValues = initialValues;
        ApplyTextures(initialStateMaterial);
        ApplyBlendedValues();

        currentState = PlanetVisualState.Frozen;
        isIlluminated = false;
        illuminationTimer = 0f;
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
        // Track illumination time
        if (isIlluminated)
        {
            illuminationTimer += Time.deltaTime;
        }
        else
        {
            illuminationTimer = 0f;
        }

        // Determine desired state
        PlanetVisualState desiredState = CalculateDesiredState();

        // If state should change
        if (desiredState != currentState)
        {
            StartTransitionToState(desiredState);
            currentState = desiredState;
        }
    }

    private PlanetVisualState CalculateDesiredState()
    {
        switch (currentState)
        {
            case PlanetVisualState.Frozen:
                // Frozen → Alive when illuminated
                return isIlluminated ? PlanetVisualState.Alive : PlanetVisualState.Frozen;

            case PlanetVisualState.Alive:
                // Alive → Burnt when illuminated long enough
                if (isIlluminated && illuminationTimer >= burnDelay)
                    return PlanetVisualState.Burnt;

                // Alive → Frozen when no longer illuminated
                return isIlluminated ? PlanetVisualState.Alive : PlanetVisualState.Frozen;

            case PlanetVisualState.Burnt:
                // Burnt → Frozen when no longer illuminated
                return isIlluminated ? PlanetVisualState.Burnt : PlanetVisualState.Frozen;

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

    private void ApplyTextures(Material stateMaterial)
    {
        if (runtimeMaterial == null || stateMaterial == null)
        {
            return;
        }

        SetTextureFromMaterialIfExists(PlanetMaskId, stateMaterial);
        SetTextureFromMaterialIfExists(PlanetMaskGradiantId, stateMaterial);
        SetTextureFromMaterialIfExists(PlanetBaseNormalId, stateMaterial);
        SetTextureFromMaterialIfExists(PlanetBaseTextureId, stateMaterial);
        SetTextureFromMaterialIfExists(PlanetLayerNormalId, stateMaterial);
        SetTextureFromMaterialIfExists(PlanetLayerTextureId, stateMaterial);
        SetTextureFromMaterialIfExists(PlanetSecondaryNormalId, stateMaterial);
        SetTextureFromMaterialIfExists(PlanetSecondaryTextureId, stateMaterial);
    }

    private void StartTransitionToState(PlanetVisualState state)
    {
        Material targetMaterial = GetStateReferenceMaterial(state);
        if (targetMaterial == null)
        {
            return;
        }

        ApplyTextures(targetMaterial);
        RuntimeBlendValues fromValues = currentValues;
        RuntimeBlendValues toValues = ToBlendValues(targetMaterial);

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
        if (runtimeMaterial == null)
        {
            return;
        }

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
    }

    private void SetTextureFromMaterialIfExists(int propertyId, Material source)
    {
        if (runtimeMaterial.HasProperty(propertyId) && source.HasProperty(propertyId))
        {
            runtimeMaterial.SetTexture(propertyId, source.GetTexture(propertyId));
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
