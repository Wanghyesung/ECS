using UnityEngine;

namespace ProceduralForceField
{
    [DisallowMultipleComponent]
    public sealed class ProceduralForceFieldHit : MonoBehaviour
    {
        #region Properties
        [SerializeField] private Renderer _targetRenderer;

        [Header("Hit Settings")]
        [SerializeField, Min(0f)] private float _hitStrength = 1.0f;

        private MaterialPropertyBlock _propertyBlock;

        private static readonly int HitPositionId = Shader.PropertyToID("_HitPosition");
        private static readonly int HitTimeId = Shader.PropertyToID("_HitTime");
        private static readonly int HitStrengthId = Shader.PropertyToID("_HitStrength");
        private static readonly int BoundsRadiusWsId = Shader.PropertyToID("_BoundsRadiusWS");
        private static readonly int HitPositionOsId = Shader.PropertyToID("_HitPositionOS");
        private static readonly int BoundsExtentsOsId = Shader.PropertyToID("_BoundsExtentsOS");
        #endregion

        #region Initialization
        private void Awake()
        {
            if (_targetRenderer == null)
            {
                _targetRenderer = GetComponent<Renderer>();
            }

            _propertyBlock = new MaterialPropertyBlock();
        }
        #endregion

        #region Methods
        public void TriggerHit(Vector3 worldPosition)
        {
            if (_targetRenderer == null)
            {
                return;
            }

            Bounds localBounds = _targetRenderer.localBounds;

            Vector3 extentsOs = localBounds.extents;
            extentsOs.x = Mathf.Max(0.0001f, extentsOs.x);
            extentsOs.y = Mathf.Max(0.0001f, extentsOs.y);
            extentsOs.z = Mathf.Max(0.0001f, extentsOs.z);

            Vector3 hitOs = _targetRenderer.transform.InverseTransformPoint(worldPosition);

            float boundsRadiusWs = Mathf.Max(0.0001f, _targetRenderer.bounds.extents.magnitude);

            _targetRenderer.GetPropertyBlock(_propertyBlock);

            _propertyBlock.SetVector(HitPositionId, new Vector4(worldPosition.x, worldPosition.y, worldPosition.z, 1.0f));
            _propertyBlock.SetFloat(HitTimeId, Time.time);
            _propertyBlock.SetFloat(HitStrengthId, _hitStrength);

            _propertyBlock.SetFloat(BoundsRadiusWsId, boundsRadiusWs);
            _propertyBlock.SetVector(HitPositionOsId, new Vector4(hitOs.x, hitOs.y, hitOs.z, 1.0f));
            _propertyBlock.SetVector(BoundsExtentsOsId, new Vector4(extentsOs.x, extentsOs.y, extentsOs.z, 0.0f));

            _targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        public void SetHitStrength(float strength)
        {
            _hitStrength = Mathf.Max(0.0f, strength);
        }
        #endregion
    }
}
