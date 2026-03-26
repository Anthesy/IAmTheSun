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
    private PlanetVisualState targetState = PlanetVisualState.Frozen;
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
            Debug.LogWarning("TransitionMaterials: No Renderer found.");
            return;
        }

        Material[] mats = targetRenderer.materials;

        if (mats.Length == 0)
        {
            Debug.LogWarning("TransitionMaterials: Renderer has no materials.");
            return;
        }

        if (materialIndex >= 0 && materialIndex < mats.Length)
        {
            runtimeMaterial = mats[materialIndex];
        }

        if (runtimeMaterial == null)
        {
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].HasProperty(TransitionWaterId))
                {
                    runtimeMaterial = mats[i];
                    materialIndex = i;
                    break;
                }
            }
        }

        if (runtimeMaterial == null)
        {
            Debug.LogWarning($"TransitionMaterials: materialIndex={materialIndex} is invalid for renderer '{targetRenderer.name}' (count={mats.Length}).");
            return;
        }

        if (!runtimeMaterial.HasProperty(TransitionWaterId))
        {
            Debug.LogWarning($"TransitionMaterials: '_TransitionWater' not found on material index {materialIndex} ('{runtimeMaterial.name}').");
            return;
        }

        Material initialStateMaterial = GetStateReferenceMaterial(PlanetVisualState.Frozen);
        RuntimeBlendValues initialValues = ToBlendValues(initialStateMaterial);
        currentValues = initialValues;
        ApplyTextures(initialStateMaterial);
        ApplyBlendedValues();

        targetState = PlanetVisualState.Frozen;
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
        // Values are blended by DOTween when target state changes.
    }

    public void SetIlluminated(bool illuminated)
    {
        isIlluminated = illuminated;

        if (!isIlluminated)
        {
            illuminationTimer = 0f;
        }
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
        PlanetVisualState newTargetState;

        if (!isIlluminated)
        {
            newTargetState = PlanetVisualState.Frozen;
        }
        else
        {
            illuminationTimer += Time.deltaTime;
            newTargetState = illuminationTimer >= burnDelay ? PlanetVisualState.Burnt : PlanetVisualState.Alive;
        }

        if (newTargetState == targetState)
        {
            return;
        }

        targetState = newTargetState;
        StartTransitionToState(targetState);
    }

    private Material GetStateReferenceMaterial(PlanetVisualState state)
    {
        switch (targetState)
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

        SetFloatIfExists(TransitionMainToSecondaryTextureId, currentValues.transitionMainToSecondaryTexture);
        SetFloatIfExists(TransitionWaterId, currentValues.transitionWater);
        SetColorIfExists(PlanetBaseColorId, currentValues.planetBaseColor);
        SetColorIfExists(PlanetLayerColorId, currentValues.planetLayerColor);
        SetColorIfExists(PlanetSecondaryColorId, currentValues.planetSecondaryColor);
    }

    private void SetFloatIfExists(int propertyId, float value)
    {
        if (runtimeMaterial.HasProperty(propertyId))
        {
            runtimeMaterial.SetFloat(propertyId, value);
        }
    }

    private void SetColorIfExists(int propertyId, Color value)
    {
        if (runtimeMaterial.HasProperty(propertyId))
        {
            runtimeMaterial.SetColor(propertyId, value);
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
