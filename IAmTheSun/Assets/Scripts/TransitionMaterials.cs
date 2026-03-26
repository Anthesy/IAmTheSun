using UnityEngine;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TransitionMaterials : MonoBehaviour
{
    private enum PlanetVisualState
    {
        Frozen,
        Alive,
        Burnt
    }



    private System.Collections.Generic.Dictionary<int, float> currentFloats = new System.Collections.Generic.Dictionary<int, float>();
    private System.Collections.Generic.Dictionary<int, Color> currentColors = new System.Collections.Generic.Dictionary<int, Color>();
    private System.Collections.Generic.Dictionary<int, Texture> currentTextures = new System.Collections.Generic.Dictionary<int, Texture>();

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

    [Header("Transition Delays")]
    [SerializeField] [Min(0f)] private float delayFrozenToAlive = 0.2f;
    [SerializeField] [Min(0f)] private float delayAliveToFrozen = 0.2f;
    [SerializeField] [Min(0f)] private float delayAliveToBurnt = 5f;
    [SerializeField] [Min(0f)] private float delayBurntToFrozen = 0.5f;
    [SerializeField] private bool debugLogging = false; // Enable to see what's being transitioned

    private Material runtimeBaseMaterial;
    private Material runtimeFresnelMaterial;
    private PlanetVisualState currentState = PlanetVisualState.Frozen;
    private PlanetVisualState pendingState = PlanetVisualState.Frozen;
    private bool isIlluminated;
    private float stateChangeTimer;
    private float aliveElapsedTime; // Time spent in Alive state
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

        if (baseMaterialIndex >= 0 && baseMaterialIndex < mats.Length && mats[baseMaterialIndex] != null)
        {
            runtimeBaseMaterial = new Material(mats[baseMaterialIndex]);
        }

        if (fresnelMaterialIndex >= 0 && fresnelMaterialIndex < mats.Length && mats[fresnelMaterialIndex] != null)
        {
            runtimeFresnelMaterial = new Material(mats[fresnelMaterialIndex]);
        }

        // Assign the material instances back to the renderer
        if (runtimeBaseMaterial != null)
        {
            mats[baseMaterialIndex] = runtimeBaseMaterial;
        }

        if (runtimeFresnelMaterial != null)
        {
            mats[fresnelMaterialIndex] = runtimeFresnelMaterial;
        }

        targetRenderer.materials = mats;

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
        ExtractAllFloatsAndColors(initialBaseStateMaterial, currentFloats, currentColors);
        ExtractAllTextures(initialBaseStateMaterial, currentTextures);
        ApplyTextures(runtimeBaseMaterial, initialBaseStateMaterial);
        ApplyTextures(runtimeFresnelMaterial, initialFresnelStateMaterial);
        ApplyAllFloatsAndColors();
        ApplyAllTextures();

        currentState = PlanetVisualState.Frozen;
        pendingState = currentState;
        isIlluminated = false;
        stateChangeTimer = 0f;
        aliveElapsedTime = 0f;
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
        // Track time spent in Alive state
        if (currentState == PlanetVisualState.Alive && isIlluminated)
        {
            aliveElapsedTime += Time.deltaTime;
            
            // Auto-transition to Burnt if delay exceeded
            if (aliveElapsedTime >= delayAliveToBurnt)
            {
                pendingState = PlanetVisualState.Burnt;
                stateChangeTimer = delayAliveToBurnt; // Bypass delay timer, go immediately
                StartTransitionToState(PlanetVisualState.Burnt);
                currentState = PlanetVisualState.Burnt;
                stateChangeTimer = 0f;
                return;
            }
        }
        else
        {
            aliveElapsedTime = 0f; // Reset when leaving Alive or not illuminated
        }

        PlanetVisualState desiredState = CalculateDesiredState();

        // If desired state is the current state, don't attempt a transition
        if (desiredState == currentState)
        {
            return;
        }

        if (pendingState != desiredState)
        {
            if (IsValidTransition(currentState, desiredState))
            {
                pendingState = desiredState;
                stateChangeTimer = 0f;
            }
            else
            {
                return;
            }
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
        aliveElapsedTime = 0f;
    }

    private PlanetVisualState CalculateDesiredState()
    {
        switch (currentState)
        {
            case PlanetVisualState.Frozen:
                return isIlluminated ? PlanetVisualState.Alive : PlanetVisualState.Frozen;

            case PlanetVisualState.Alive:
                return !isIlluminated ? PlanetVisualState.Frozen : PlanetVisualState.Alive;

            case PlanetVisualState.Burnt:
                return !isIlluminated ? PlanetVisualState.Frozen : PlanetVisualState.Burnt;

            default:
                return PlanetVisualState.Frozen;
        }
    }

    private bool IsValidTransition(PlanetVisualState from, PlanetVisualState to)
    {
        if (from == PlanetVisualState.Frozen && to == PlanetVisualState.Alive) return true;
        if (from == PlanetVisualState.Alive && to == PlanetVisualState.Frozen) return true;
        if (from == PlanetVisualState.Alive && to == PlanetVisualState.Burnt) return true;
        if (from == PlanetVisualState.Burnt && to == PlanetVisualState.Frozen) return true;
        return false;
    }

    private float GetTransitionDelay(PlanetVisualState from, PlanetVisualState to)
    {
        if (from == PlanetVisualState.Frozen && to == PlanetVisualState.Alive)
            return delayFrozenToAlive;
        if (from == PlanetVisualState.Alive && to == PlanetVisualState.Frozen)
            return delayAliveToFrozen;
        if (from == PlanetVisualState.Alive && to == PlanetVisualState.Burnt)
            return delayAliveToBurnt;
        if (from == PlanetVisualState.Burnt && to == PlanetVisualState.Frozen)
            return delayBurntToFrozen;
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

    private void ExtractAllFloatsAndColors(Material source, System.Collections.Generic.Dictionary<int, float> outFloats, System.Collections.Generic.Dictionary<int, Color> outColors)
    {
        if (source == null)
        {
            return;
        }

        outFloats.Clear();
        outColors.Clear();

#if UNITY_EDITOR
        // In editor, use ShaderUtil to get all properties
        Shader shader = source.shader;
        int propertyCount = ShaderUtil.GetPropertyCount(shader);

        for (int i = 0; i < propertyCount; i++)
        {
            string propName = ShaderUtil.GetPropertyName(shader, i);
            int propId = Shader.PropertyToID(propName);
            ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(shader, i);

            if (propType == ShaderUtil.ShaderPropertyType.Float && source.HasProperty(propId))
            {
                float value = source.GetFloat(propId);
                outFloats[propId] = value;
                if (debugLogging)
                    Debug.Log($"[TransitionMaterials] Extracted FLOAT: {propName} = {value}");
            }
            else if (propType == ShaderUtil.ShaderPropertyType.Color && source.HasProperty(propId))
            {
                Color value = source.GetColor(propId);
                outColors[propId] = value;
                if (debugLogging)
                    Debug.Log($"[TransitionMaterials] Extracted COLOR: {propName} = {value}");
            }
        }
#else
        // Runtime fallback: complete list from the shader
        string[] floatProperties = new string[]
        {
            "_TransitionMainToSecondaryTexture",
            "_TransitionWater",
            "_WaterOpacity"
        };

        foreach (string propName in floatProperties)
        {
            int propId = Shader.PropertyToID(propName);
            if (source.HasProperty(propId))
            {
                float value = source.GetFloat(propId);
                outFloats[propId] = value;
                if (debugLogging)
                    Debug.Log($"[TransitionMaterials] Extracted FLOAT: {propName} = {value}");
            }
        }

        string[] colorProperties = new string[]
        {
            "_PlanetBaseColor",
            "_PlanetWaterColor",
            "_PlanetSecondaryColor"
        };

        foreach (string propName in colorProperties)
        {
            int propId = Shader.PropertyToID(propName);
            if (source.HasProperty(propId))
            {
                Color value = source.GetColor(propId);
                outColors[propId] = value;
                if (debugLogging)
                    Debug.Log($"[TransitionMaterials] Extracted COLOR: {propName} = {value}");
            }
        }
#endif
    }

    private void ExtractAllTextures(Material source, System.Collections.Generic.Dictionary<int, Texture> outTextures)
    {
        if (source == null)
        {
            return;
        }

        outTextures.Clear();

#if UNITY_EDITOR
        // In editor, use ShaderUtil to get all texture properties
        Shader shader = source.shader;
        int propertyCount = ShaderUtil.GetPropertyCount(shader);

        for (int i = 0; i < propertyCount; i++)
        {
            string propName = ShaderUtil.GetPropertyName(shader, i);
            int propId = Shader.PropertyToID(propName);
            ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(shader, i);

            if (propType == ShaderUtil.ShaderPropertyType.TexEnv && source.HasProperty(propId))
            {
                Texture tex = source.GetTexture(propId);
                if (tex != null)
                {
                    outTextures[propId] = tex;
                }
            }
        }
#else
        // Runtime fallback: complete list from the shader
        string[] textureProperties = new string[]
        {
            "_PlanetMask",
            "_PlanetMaskGradiant",
            "_PlanetBaseNormal",
            "_PlanetBaseTexture",
            "_PlanetWaterNormal",
            "_PlanetWaterTexture",
            "_PlanetSecondaryNormal",
            "_PlanetSecondaryTexture"
        };

        foreach (string propName in textureProperties)
        {
            int propId = Shader.PropertyToID(propName);
            if (source.HasProperty(propId))
            {
                Texture tex = source.GetTexture(propId);
                if (tex != null)
                {
                    outTextures[propId] = tex;
                }
            }
        }
#endif
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
        // Ensure this is a valid transition
        if (!IsValidTransition(currentState, state))
        {
            Debug.LogWarning($"Attempted invalid transition: {currentState} -> {state}");
            return;
        }

        Material targetBaseMaterial = GetBaseStateReferenceMaterial(state);
        Material targetFresnelMaterial = GetFresnelStateReferenceMaterial(state);

        if (targetBaseMaterial == null || targetFresnelMaterial == null)
        {
            return;
        }

        // Apply textures immediately
        ApplyTextures(runtimeBaseMaterial, targetBaseMaterial);
        ApplyTextures(runtimeFresnelMaterial, targetFresnelMaterial);

        // Create snapshots of current and target materials
        var fromBaseMat = new Material(runtimeBaseMaterial);
        var fromFresnelMat = new Material(runtimeFresnelMaterial);

        if (activeTransitionTween != null && activeTransitionTween.IsActive())
        {
            activeTransitionTween.Kill();
        }

        float tweenProgress = 0f;
        float safeDuration = Mathf.Max(0.01f, transitionDuration);

        activeTransitionTween = DOTween.To(() => tweenProgress, x =>
        {
            tweenProgress = x;
            
            // Blend base material: copy all properties from target, lerped
            BlendMaterialProperties(runtimeBaseMaterial, fromBaseMat, targetBaseMaterial, tweenProgress);
            
            // Blend fresnel material: copy all properties from target, lerped
            BlendMaterialProperties(runtimeFresnelMaterial, fromFresnelMat, targetFresnelMaterial, tweenProgress);
            
            if (debugLogging)
                Debug.Log($"[TransitionMaterials] Transition progress: {tweenProgress:F2}");
        }, 1f, safeDuration)
        .SetEase(transitionEase)
        .SetLink(gameObject)
        .OnComplete(() =>
        {
            // Final blend to ensure perfect match
            BlendMaterialProperties(runtimeBaseMaterial, fromBaseMat, targetBaseMaterial, 1f);
            BlendMaterialProperties(runtimeFresnelMaterial, fromFresnelMat, targetFresnelMaterial, 1f);
            
            Destroy(fromBaseMat);
            Destroy(fromFresnelMat);
            
            if (debugLogging)
                Debug.Log($"[TransitionMaterials] Transition complete to state: {state}");
        });
    }

    private void BlendMaterialProperties(Material destination, Material fromMat, Material toMat, float t)
    {
        if (destination == null || fromMat == null || toMat == null)
            return;

#if UNITY_EDITOR
        // In editor: get all properties from shader
        Shader shader = toMat.shader;
        int propertyCount = ShaderUtil.GetPropertyCount(shader);

        for (int i = 0; i < propertyCount; i++)
        {
            string propName = ShaderUtil.GetPropertyName(shader, i);
            int propId = Shader.PropertyToID(propName);
            ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(shader, i);

            if (propType == ShaderUtil.ShaderPropertyType.Float && destination.HasProperty(propId))
            {
                float fromVal = fromMat.HasProperty(propId) ? fromMat.GetFloat(propId) : 0f;
                float toVal = toMat.HasProperty(propId) ? toMat.GetFloat(propId) : 0f;
                destination.SetFloat(propId, Mathf.Lerp(fromVal, toVal, t));
            }
            else if (propType == ShaderUtil.ShaderPropertyType.Color && destination.HasProperty(propId))
            {
                Color fromVal = fromMat.HasProperty(propId) ? fromMat.GetColor(propId) : Color.white;
                Color toVal = toMat.HasProperty(propId) ? toMat.GetColor(propId) : Color.white;
                destination.SetColor(propId, Color.Lerp(fromVal, toVal, t));
            }
        }
#else
        // Runtime fallback: blend all common properties
        string[] allProperties = new string[]
        {
            "_TransitionMainToSecondaryTexture", "_TransitionWater", "_WaterOpacity",
            "_PlanetBaseColor", "_PlanetWaterColor", "_PlanetSecondaryColor"
        };

        foreach (string propName in allProperties)
        {
            int propId = Shader.PropertyToID(propName);
            if (destination.HasProperty(propId))
            {
                try
                {
                    float fromVal = fromMat.HasProperty(propId) ? fromMat.GetFloat(propId) : 0f;
                    float toVal = toMat.HasProperty(propId) ? toMat.GetFloat(propId) : 0f;
                    destination.SetFloat(propId, Mathf.Lerp(fromVal, toVal, t));
                }
                catch { }
            }
        }

        string[] colorProperties = new string[]
        {
            "_PlanetBaseColor", "_PlanetWaterColor", "_PlanetSecondaryColor"
        };

        foreach (string propName in colorProperties)
        {
            int propId = Shader.PropertyToID(propName);
            if (destination.HasProperty(propId))
            {
                try
                {
                    Color fromVal = fromMat.HasProperty(propId) ? fromMat.GetColor(propId) : Color.white;
                    Color toVal = toMat.HasProperty(propId) ? toMat.GetColor(propId) : Color.white;
                    destination.SetColor(propId, Color.Lerp(fromVal, toVal, t));
                }
                catch { }
            }
        }
#endif
    }

    private void ApplyAllFloatsAndColors()
    {
        if (runtimeBaseMaterial == null)
        {
            return;
        }

        foreach (var kvp in currentFloats)
        {
            if (runtimeBaseMaterial.HasProperty(kvp.Key))
            {
                runtimeBaseMaterial.SetFloat(kvp.Key, kvp.Value);
                if (debugLogging)
                    Debug.Log($"[TransitionMaterials] Applied FLOAT: PropertyID {kvp.Key} = {kvp.Value}");
            }
            else if (debugLogging)
            {
                Debug.LogWarning($"[TransitionMaterials] Material missing property: {kvp.Key}");
            }
        }

        foreach (var kvp in currentColors)
        {
            if (runtimeBaseMaterial.HasProperty(kvp.Key))
            {
                runtimeBaseMaterial.SetColor(kvp.Key, kvp.Value);
                if (debugLogging)
                    Debug.Log($"[TransitionMaterials] Applied COLOR: PropertyID {kvp.Key} = {kvp.Value}");
            }
            else if (debugLogging)
            {
                Debug.LogWarning($"[TransitionMaterials] Material missing property: {kvp.Key}");
            }
        }
    }

    private void ApplyAllTextures()
    {
        if (runtimeBaseMaterial == null)
        {
            return;
        }

        foreach (var kvp in currentTextures)
        {
            if (runtimeBaseMaterial.HasProperty(kvp.Key) && kvp.Value != null)
            {
                runtimeBaseMaterial.SetTexture(kvp.Key, kvp.Value);
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

        // Clean up material instances
        if (runtimeBaseMaterial != null)
        {
            Destroy(runtimeBaseMaterial);
        }

        if (runtimeFresnelMaterial != null)
        {
            Destroy(runtimeFresnelMaterial);
        }
    }
}
