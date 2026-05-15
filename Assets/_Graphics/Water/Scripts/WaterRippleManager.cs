using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace Ledsna.Water
{
    [RequireComponent(typeof(Renderer))]
    public class WaterRippleManager : MonoBehaviour
    {
        [Header("Ripple Settings")]
        [SerializeField] private int maxRipples = 64;

        [SerializeField] private float baseRippleLifetime = 1.5f;

        [SerializeField] private float speedLifetimeMultiplier = 1.0f;

        [SerializeField] private float rippleSpawnInterval = 0.05f;

        [SerializeField] private float minSpawnDistance = 0.2f;

        [SerializeField] private float minRippleSpeed = 0.3f;

        [Header("Ripple Appearance")]
        [SerializeField] private float rippleSpeed = 0.7f;
        [SerializeField] private float rippleFrequency = 10.0f;
        [SerializeField] private float rippleAmplitude = 0.035f;

        [Header("Auto-Detection")]
        [SerializeField] private LayerMask rippleObjectLayers = -1;

        [Header("Simulation")]
        [SerializeField] private int simulationResolution = 256;
        [SerializeField] private float simulationSize = 50.0f;
        [SerializeField] private float cameraRearPadding = 2.0f;

        private Renderer waterRenderer;
        private Material waterMaterial;
        private List<RippleData> activeRipples = new List<RippleData>();
        private Dictionary<GameObject, float> lastRippleTime = new Dictionary<GameObject, float>();
        private Dictionary<GameObject, Vector3> lastPosition = new Dictionary<GameObject, Vector3>();

        private RenderTexture rippleMap;
        private Material stampMaterial;
        private Mesh quadMesh;
        private CommandBuffer cmd;

        private class RippleData
        {
            public Vector3 position;
            public float spawnTime;
            public Vector3 direction;
            public float speed;
            public float maxAge;
        }

        private void Awake()
        {
            waterRenderer = GetComponent<Renderer>();
            waterMaterial = waterRenderer.material;

            Shader stampShader = Shader.Find("Hidden/RippleStamp");
            if (stampShader == null)
            {
                Debug.LogError("Could not find shader 'Hidden/RippleStamp'. Please ensure it is included in the build.");
                enabled = false;
                return;
            }
            stampMaterial = new Material(stampShader);

            GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempQuad);

            InitializeRippleMap();
        }

        private void InitializeRippleMap()
        {
            if (rippleMap != null) rippleMap.Release();
            rippleMap = new RenderTexture(simulationResolution, simulationResolution, 0, RenderTextureFormat.RHalf);
            rippleMap.name = "RippleMap";
            rippleMap.wrapMode = TextureWrapMode.Clamp;
            rippleMap.filterMode = FilterMode.Bilinear;
            rippleMap.Create();
        }

        private void Update()
        {
            activeRipples.RemoveAll(ripple => Time.time - ripple.spawnTime > ripple.maxAge + 0.1f);

            UpdateRippleMap();
        }

        public void CreateRipple(Vector3 worldPosition, Vector3 direction, float speed)
        {
            if (speed < minRippleSpeed) return;

            if (activeRipples.Count >= maxRipples)
            {
                activeRipples.RemoveAt(0);
            }

            float speedNorm = speed * 0.2f;
            float calculatedMaxAge = baseRippleLifetime + speedNorm * speedLifetimeMultiplier;

            activeRipples.Add(new RippleData
            {
                position = worldPosition,
                spawnTime = Time.time,
                direction = direction.normalized,
                speed = speed,
                maxAge = calculatedMaxAge
            });

        }

        public void CreateRippleFromObject(GameObject obj, Vector3 worldPosition)
        {
            if (lastRippleTime.TryGetValue(obj, out float lastTime))
            {
                if (Time.time - lastTime < rippleSpawnInterval)
                    return;
            }

            if (lastPosition.TryGetValue(obj, out Vector3 lastPos))
            {
                if ((worldPosition - lastPos).sqrMagnitude < minSpawnDistance * minSpawnDistance)
                    return;
            }

            Vector3 currentPos = worldPosition;
            Vector3 velocity = Vector3.zero;
            float speed = 0f;

            if (lastPosition.TryGetValue(obj, out Vector3 prevPos))
            {
                float deltaTime = Time.time - lastTime;
                if (deltaTime > 0.001f)
                {
                    velocity = (currentPos - prevPos) / deltaTime;
                    speed = velocity.magnitude;
                }
            }

            lastRippleTime[obj] = Time.time;
            lastPosition[obj] = currentPos;

            if (speed >= minRippleSpeed)
            {
                Vector3 compensatedPos = worldPosition + velocity.normalized * (speed * 0.05f);
                compensatedPos.y = worldPosition.y;
                CreateRipple(compensatedPos, velocity, speed);
            }
        }

        private void UpdateRippleMap()
        {
            if (waterMaterial == null || rippleMap == null) return;

            Vector3 camPos = Vector3.zero;
            Vector3 camForward = Vector3.forward;

            if (Camera.main != null)
            {
                camPos = Camera.main.transform.position;
                camForward = Camera.main.transform.forward;
            }

            Vector3 forwardXZ = new Vector3(camForward.x, 0, camForward.z).normalized;
            if (forwardXZ.sqrMagnitude < 0.001f) forwardXZ = Vector3.forward;

            float offsetDist = (simulationSize * 0.5f) - cameraRearPadding;
            Vector3 targetCenter = camPos + forwardXZ * offsetDist;

            Vector3 origin = new Vector3(targetCenter.x, 0, targetCenter.z);

            if (cmd == null) cmd = new CommandBuffer { name = "UpdateRippleMap" };
            cmd.Clear();

            cmd.BeginSample("Render Ripples");

            cmd.SetRenderTarget(rippleMap);
            cmd.ClearRenderTarget(false, true, Color.black);

            float halfSize = simulationSize * 0.5f;
            Matrix4x4 proj = Matrix4x4.Ortho(-halfSize, halfSize, -halfSize, halfSize, -100, 100);
            Matrix4x4 viewMatrix = Matrix4x4.Inverse(Matrix4x4.TRS(new Vector3(origin.x, 10, origin.z), Quaternion.Euler(90, 0, 0), Vector3.one));
            cmd.SetViewProjectionMatrices(viewMatrix, proj);

            float currentTime = Time.time;

            float rSpeed = rippleSpeed;
            float rFreq = rippleFrequency;
            float rAmp = rippleAmplitude;

            foreach (var ripple in activeRipples)
            {
                float age = currentTime - ripple.spawnTime;
                if (age < 0) continue;

                float speed = ripple.speed;
                float speedNorm = speed * 0.2f;
                float effectiveSpeed = rSpeed * (0.7f + 0.6f * Mathf.Min(speedNorm * 1.5f, 1.0f));

                float radius = age * effectiveSpeed + 2.0f;
                float size = radius * 2.0f;

                MaterialPropertyBlock props = new MaterialPropertyBlock();
                props.SetFloat("_Age", age);
                props.SetFloat("_MaxAge", ripple.maxAge);
                props.SetFloat("_Speed", effectiveSpeed);
                props.SetFloat("_Amplitude", rAmp * (0.5f + Mathf.Min(speedNorm * 1.5f, 1.0f)));
                props.SetFloat("_Frequency", rFreq);
                props.SetFloat("_QuadRadius", radius);

                Matrix4x4 model = Matrix4x4.TRS(ripple.position, Quaternion.Euler(90, 0, 0), new Vector3(size, size, 1));

                cmd.DrawMesh(quadMesh, model, stampMaterial, 0, 0, props);
            }

            cmd.EndSample("Render Ripples");
            Graphics.ExecuteCommandBuffer(cmd);

            waterMaterial.SetTexture("_RippleMap", rippleMap);
            waterMaterial.SetVector("_RippleMapOrigin", new Vector4(origin.x, origin.z, simulationSize, 0));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & rippleObjectLayers) == 0) return;

            Vector3 contactPoint = other.transform.position;
            contactPoint.y = transform.position.y;

            lastPosition[other.gameObject] = contactPoint;
            lastRippleTime[other.gameObject] = Time.time;
        }

        private void OnTriggerStay(Collider other)
        {
            if (((1 << other.gameObject.layer) & rippleObjectLayers) == 0) return;

            Vector3 contactPoint = other.transform.position;
            contactPoint.y = transform.position.y;

            CreateRippleFromObject(other.gameObject, contactPoint);
        }

        private void OnDestroy()
        {
            if (rippleMap != null) rippleMap.Release();
            if (cmd != null) cmd.Release();

            if (waterMaterial != null && waterRenderer.sharedMaterial != waterMaterial)
            {
                if (Application.isPlaying)
                    Destroy(waterMaterial);
                else
                    DestroyImmediate(waterMaterial);
            }
        }

        private void OnDrawGizmosSelected()
        {
            foreach (var ripple in activeRipples)
            {
                float age = Time.time - ripple.spawnTime;
                float alpha = 1f - (age / ripple.maxAge);

                Gizmos.color = new Color(0f, 1f, 1f, alpha);
                Gizmos.DrawWireSphere(ripple.position, 0.2f);

                Gizmos.color = new Color(1f, 1f, 0f, alpha);
                Vector3 arrowEnd = ripple.position + ripple.direction * 1f;
                Gizmos.DrawLine(ripple.position, arrowEnd);

                float speedNorm = ripple.speed * 0.2f;
                float effectiveSpeed = rippleSpeed * (0.7f + 0.6f * Mathf.Min(speedNorm * 1.5f, 1.0f));
                float radius = age * effectiveSpeed + 2.0f;

                Gizmos.color = new Color(0f, 1f, 1f, alpha * 0.5f);
                int segments = 32;
                float angleStep = 360f / segments;
                Vector3 prevPoint = ripple.position + new Vector3(radius, 0, 0);

                for (int i = 1; i <= segments; i++)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    Vector3 nextPoint = ripple.position + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    Gizmos.DrawLine(prevPoint, nextPoint);
                    prevPoint = nextPoint;
                }
            }
        }
    }
}
