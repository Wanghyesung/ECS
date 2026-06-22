using UnityEngine;

namespace VolumetricLines
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [ExecuteInEditMode]
    public class VolumetricLineBehavior : MonoBehaviour
    {
        static readonly Vector3 Average = new Vector3(1f / 3f, 1f / 3f, 1f / 3f);

        #region private variables
        [SerializeField] public Material m_templateMaterial;
        [SerializeField] private bool m_doNotOverwriteTemplateMaterialProperties;
        [SerializeField] private Vector3 m_startPos;
        [SerializeField] private Vector3 m_endPos = new Vector3(0f, 0f, 100f);
        [SerializeField] private Color m_lineColor;
        [SerializeField] private float m_lineWidth;
        [SerializeField][Range(0f, 1f)] private float m_lightSaberFactor;

        private MeshFilter m_meshFilter;
        private MeshRenderer m_meshRenderer;
        private MaterialPropertyBlock m_propBlock;
        #endregion

        #region properties
        public Material TemplateMaterial
        {
            get { return m_templateMaterial; }
            set { m_templateMaterial = value; }
        }

        public bool DoNotOverwriteTemplateMaterialProperties
        {
            get { return m_doNotOverwriteTemplateMaterialProperties; }
            set { m_doNotOverwriteTemplateMaterialProperties = value; }
        }

        public Color LineColor
        {
            get { return m_lineColor; }
            set { m_lineColor = value; ApplyProperties(); }
        }

        public float LineWidth
        {
            get { return m_lineWidth; }
            set { m_lineWidth = value; ApplyProperties(); UpdateBounds(); }
        }

        public float LightSaberFactor
        {
            get { return m_lightSaberFactor; }
            set { m_lightSaberFactor = value; ApplyProperties(); }
        }

        public Vector3 StartPos
        {
            get { return m_startPos; }
            set { m_startPos = value; SetStartAndEndPoints(m_startPos, m_endPos); }
        }

        public Vector3 EndPos
        {
            get { return m_endPos; }
            set { m_endPos = value; SetStartAndEndPoints(m_startPos, m_endPos); }
        }
        #endregion

        #region methods
        private void ApplyProperties()
        {
            if (m_meshRenderer == null) return;

            // 머티리얼 clone 없이 template 공유
            if (m_meshRenderer.sharedMaterial != m_templateMaterial)
            {
                m_meshRenderer.sharedMaterial = m_templateMaterial;
            }

            if (m_doNotOverwriteTemplateMaterialProperties) return;

            if (m_propBlock == null) m_propBlock = new MaterialPropertyBlock();
            m_meshRenderer.GetPropertyBlock(m_propBlock);

            m_propBlock.SetColor("_Color", m_lineColor);
            m_propBlock.SetFloat("_LineWidth", m_lineWidth);
            m_propBlock.SetFloat("_LightSaberFactor", m_lightSaberFactor);
            m_propBlock.SetFloat("_LineScale", Vector3.Dot(transform.lossyScale, Average));

            m_meshRenderer.SetPropertyBlock(m_propBlock);
        }

        public void UpdateLineScale()
        {
            ApplyProperties();
        }

        private Bounds CalculateBounds()
        {
            var maxWidth = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            var scaledLineWidth = maxWidth * m_lineWidth * 0.5f;

            var min = new Vector3(
                Mathf.Min(m_startPos.x, m_endPos.x) - scaledLineWidth,
                Mathf.Min(m_startPos.y, m_endPos.y) - scaledLineWidth,
                Mathf.Min(m_startPos.z, m_endPos.z) - scaledLineWidth
            );
            var max = new Vector3(
                Mathf.Max(m_startPos.x, m_endPos.x) + scaledLineWidth,
                Mathf.Max(m_startPos.y, m_endPos.y) + scaledLineWidth,
                Mathf.Max(m_startPos.z, m_endPos.z) + scaledLineWidth
            );

            return new Bounds { min = min, max = max };
        }

        public void UpdateBounds()
        {
            if (null != m_meshFilter)
            {
                var mesh = m_meshFilter.sharedMesh;
                Debug.Assert(null != mesh);
                if (null != mesh)
                {
                    mesh.bounds = CalculateBounds();
                }
            }
        }

        public void SetStartAndEndPoints(Vector3 startPoint, Vector3 endPoint)
        {
            m_startPos = startPoint;
            m_endPos = endPoint;

            Vector3[] vertexPositions = {
                m_startPos, m_startPos, m_startPos, m_startPos,
                m_endPos,   m_endPos,   m_endPos,   m_endPos,
            };

            Vector3[] other = {
                m_endPos,   m_endPos,   m_endPos,   m_endPos,
                m_startPos, m_startPos, m_startPos, m_startPos,
            };

            if (null != m_meshFilter)
            {
                var mesh = m_meshFilter.sharedMesh;
                Debug.Assert(null != mesh);
                if (null != mesh)
                {
                    mesh.vertices = vertexPositions;
                    mesh.normals = other;
                    UpdateBounds();
                }
            }
        }
        #endregion

        #region event functions
        void Start()
        {
            m_meshFilter = GetComponent<MeshFilter>();
            m_meshRenderer = GetComponent<MeshRenderer>();
            m_propBlock = new MaterialPropertyBlock();

            Mesh mesh = new Mesh();
            m_meshFilter.mesh = mesh;
            SetStartAndEndPoints(m_startPos, m_endPos);
            mesh.uv = VolumetricLineVertexData.TexCoords;
            mesh.uv2 = VolumetricLineVertexData.VertexOffsets;
            mesh.SetIndices(VolumetricLineVertexData.Indices, MeshTopology.Triangles, 0);

            ApplyProperties();
        }

        void OnDestroy()
        {
            if (null != m_meshFilter)
            {
                if (Application.isPlaying) Mesh.Destroy(m_meshFilter.sharedMesh);
                else Mesh.DestroyImmediate(m_meshFilter.sharedMesh);
                m_meshFilter.sharedMesh = null;
            }
            // clone 안 만들었으니 DestroyMaterial() 불필요
        }

        void Update()
        {
            if (transform.hasChanged)
            {
                ApplyProperties();  // UpdateLineScale 포함
                UpdateBounds();
            }
        }

        void OnValidate()
        {
            if (string.IsNullOrEmpty(gameObject.scene.name) ||
                string.IsNullOrEmpty(gameObject.scene.path)) return;

            m_meshRenderer = GetComponent<MeshRenderer>();
            ApplyProperties();
            UpdateBounds();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(
                gameObject.transform.TransformPoint(m_startPos),
                gameObject.transform.TransformPoint(m_endPos)
            );
        }
        #endregion
    }
}