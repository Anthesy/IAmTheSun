using UnityEngine;

public class TransitionMaterials : MonoBehaviour
{
    private enum PlanetVisualState
    {
        Frozen,
        Alive,
        Burnt
    }

    [System.Serializable]
    private class PlanetShaderState
    {
        [Header("Transitions")]
        public float transitionMainToSecondaryTexture = 0f;
        public float transitionWater = 0f;

        [Header("Masks")]
        public Texture2D planetMask;
        public Texture2D planetMaskGradiant;

        [Header("Base")]
        public Texture2D planetBaseNormal;
        public Texture2D planetBaseTexture;
        public Color planetBaseColor = Color.white;

        [Header("Layer")]
        public Texture2D planetLayerNormal;
        public Texture2D planetLayerTexture;
        public Color planetLayerColor = Color.white;

        [Header("Secondary")]
        public Texture2D planetSecondaryNormal;
        public Texture2D planetSecondaryTexture;
        public Color planetSecondaryColor = Color.white;
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
    [SerializeField] private float transitionSpeed = 2f;
    [SerializeField] private float burnDelay = 5f;

    [Header("Frozen State")]
    [SerializeField] private PlanetShaderState frozenState = new PlanetShaderState();

    [Header("Alive State")]
    [SerializeField] private PlanetShaderState aliveState = new PlanetShaderState();

    [Header("Burnt State")]
    [SerializeField] private PlanetShaderState burntState = new PlanetShaderState();

    private Material runtimeMaterial;
    private RuntimeBlendValues currentValues;
    private PlanetVisualState targetState = PlanetVisualState.Frozen;
    private bool isIlluminated;
    private float illuminationTimer;
    private bool isInitialized;

    private void Reset()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        ApplyShaderDefaultsToAllStates();
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

        RuntimeBlendValues initialValues = ToBlendValues(frozenState);
        currentValues = initialValues;
        ApplyTextures(frozenState);
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
        BlendTowardTargetState();
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
        ApplyTextures(GetTargetStateData());
    }

    private void BlendTowardTargetState()
    {
        PlanetShaderState targetData = GetTargetStateData();
        RuntimeBlendValues targetValues = ToBlendValues(targetData);

        float floatStep = transitionSpeed * Time.deltaTime;
        float colorLerpT = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);

        currentValues.transitionMainToSecondaryTexture = Mathf.MoveTowards(currentValues.transitionMainToSecondaryTexture, targetValues.transitionMainToSecondaryTexture, floatStep);
        currentValues.transitionWater = Mathf.MoveTowards(currentValues.transitionWater, targetValues.transitionWater, floatStep);
        currentValues.planetBaseColor = Color.Lerp(currentValues.planetBaseColor, targetValues.planetBaseColor, colorLerpT);
        currentValues.planetLayerColor = Color.Lerp(currentValues.planetLayerColor, targetValues.planetLayerColor, colorLerpT);
        currentValues.planetSecondaryColor = Color.Lerp(currentValues.planetSecondaryColor, targetValues.planetSecondaryColor, colorLerpT);

        ApplyBlendedValues();
    }

    private PlanetShaderState GetTargetStateData()
    {
        switch (targetState)
        {
            case PlanetVisualState.Alive:
                return aliveState;
            case PlanetVisualState.Burnt:
                return burntState;
            default:
                return frozenState;
        }
    }

    private RuntimeBlendValues ToBlendValues(PlanetShaderState state)
    {
        return new RuntimeBlendValues
        {
            transitionMainToSecondaryTexture = state.transitionMainToSecondaryTexture,
            transitionWater = state.transitionWater,
            planetBaseColor = state.planetBaseColor,
            planetLayerColor = state.planetLayerColor,
            planetSecondaryColor = state.planetSecondaryColor
        };
    }

    private void ApplyTextures(PlanetShaderState state)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        SetTextureIfExists(PlanetMaskId, state.planetMask);
        SetTextureIfExists(PlanetMaskGradiantId, state.planetMaskGradiant);
        SetTextureIfExists(PlanetBaseNormalId, state.planetBaseNormal);
        SetTextureIfExists(PlanetBaseTextureId, state.planetBaseTexture);
        SetTextureIfExists(PlanetLayerNormalId, state.planetLayerNormal);
        SetTextureIfExists(PlanetLayerTextureId, state.planetLayerTexture);
        SetTextureIfExists(PlanetSecondaryNormalId, state.planetSecondaryNormal);
        SetTextureIfExists(PlanetSecondaryTextureId, state.planetSecondaryTexture);
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

    private void SetTextureIfExists(int propertyId, Texture value)
    {
        if (runtimeMaterial.HasProperty(propertyId))
        {
            runtimeMaterial.SetTexture(propertyId, value);
        }
    }

    private void ApplyShaderDefaultsToAllStates()
    {
        if (!TryGetReferenceMaterial(out Material referenceMaterial))
        {
            return;
        }

        Material shaderDefaultsMaterial = new Material(referenceMaterial.shader);

        CopyStateFromMaterial(shaderDefaultsMaterial, frozenState);
        CopyStateFromMaterial(shaderDefaultsMaterial, aliveState);
        CopyStateFromMaterial(shaderDefaultsMaterial, burntState);

#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Destroy(shaderDefaultsMaterial);
        }
        else
        {
            DestroyImmediate(shaderDefaultsMaterial);
        }
#else
        Destroy(shaderDefaultsMaterial);
#endif
    }

    private bool TryGetReferenceMaterial(out Material referenceMaterial)
    {
        referenceMaterial = null;

        if (targetRenderer == null)
        {
            return false;
        }

        Material[] mats = targetRenderer.sharedMaterials;
        if (mats == null || mats.Length == 0)
        {
            return false;
        }

        if (materialIndex >= 0 && materialIndex < mats.Length)
        {
            referenceMaterial = mats[materialIndex];
        }

        if (referenceMaterial == null)
        {
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].HasProperty(TransitionWaterId))
                {
                    referenceMaterial = mats[i];
                    materialIndex = i;
                    break;
                }
            }
        }

        return referenceMaterial != null;
    }

    private void CopyStateFromMaterial(Material source, PlanetShaderState target)
    {
        target.transitionMainToSecondaryTexture = source.HasProperty(TransitionMainToSecondaryTextureId)
            ? source.GetFloat(TransitionMainToSecondaryTextureId)
            : 0f;

        target.transitionWater = source.HasProperty(TransitionWaterId)
            ? source.GetFloat(TransitionWaterId)
            : 0f;

        target.planetMask = source.HasProperty(PlanetMaskId)
            ? source.GetTexture(PlanetMaskId) as Texture2D
            : null;

        target.planetMaskGradiant = source.HasProperty(PlanetMaskGradiantId)
            ? source.GetTexture(PlanetMaskGradiantId) as Texture2D
            : null;

        target.planetBaseNormal = source.HasProperty(PlanetBaseNormalId)
            ? source.GetTexture(PlanetBaseNormalId) as Texture2D
            : null;

        target.planetBaseTexture = source.HasProperty(PlanetBaseTextureId)
            ? source.GetTexture(PlanetBaseTextureId) as Texture2D
            : null;

        target.planetBaseColor = source.HasProperty(PlanetBaseColorId)
            ? source.GetColor(PlanetBaseColorId)
            : Color.white;

        target.planetLayerNormal = source.HasProperty(PlanetLayerNormalId)
            ? source.GetTexture(PlanetLayerNormalId) as Texture2D
            : null;

        target.planetLayerTexture = source.HasProperty(PlanetLayerTextureId)
            ? source.GetTexture(PlanetLayerTextureId) as Texture2D
            : null;

        target.planetLayerColor = source.HasProperty(PlanetLayerColorId)
            ? source.GetColor(PlanetLayerColorId)
            : Color.white;

        target.planetSecondaryNormal = source.HasProperty(PlanetSecondaryNormalId)
            ? source.GetTexture(PlanetSecondaryNormalId) as Texture2D
            : null;

        target.planetSecondaryTexture = source.HasProperty(PlanetSecondaryTextureId)
            ? source.GetTexture(PlanetSecondaryTextureId) as Texture2D
            : null;

        target.planetSecondaryColor = source.HasProperty(PlanetSecondaryColorId)
            ? source.GetColor(PlanetSecondaryColorId)
            : Color.white;
    }
}
