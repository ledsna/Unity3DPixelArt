using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class WriteRendererID : MonoBehaviour
{
    [SerializeField] private bool useOverrideID;

    [SerializeField] private uint overrideID;

    static uint s_Counter = 999;

    public static uint GetNextID() => ++s_Counter;

    void OnEnable()
    {
        uint id;

        if (useOverrideID)
        {
            id = overrideID;
        }
        else
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                id = unchecked((uint)gameObject.GetInstanceID());
            }
            else
#endif
            {
                id = ++s_Counter;
            }
        }

        if (TryGetComponent<MeshRenderer>(out var mr))
            mr.SetShaderUserValue(id);
        else if (TryGetComponent<SkinnedMeshRenderer>(out var smr))
            smr.SetShaderUserValue(id);
    }

    void OnDisable()
    {
        uint id = 0;
        if (TryGetComponent<MeshRenderer>(out var mr))
            mr.SetShaderUserValue(id);
        else if (TryGetComponent<SkinnedMeshRenderer>(out var smr))
            smr.SetShaderUserValue(id);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (isActiveAndEnabled)
        {
            OnEnable();
        }
    }
#endif

}
