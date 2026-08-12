using UnityEngine;

namespace SCJam.BoardSystem
{
    public class BoardView : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private Transform _gridOrigin;
        [SerializeField, Min(0.1f)] private float _cellSize = 2f;
        [SerializeField] private bool _areGizmosEnabled = true;
        [SerializeField] private Color _gizmoColor = Color.green;


        // ===== Private Fields ===== //

        private BoardGrid _boardGrid;


        // ===== Public Properties ===== //

        public float CellSize => _cellSize;
        public Transform GridOrigin => _gridOrigin != null ? _gridOrigin : transform;


        // ===== Methods ===== //

        public void Initialize(BoardGrid boardGrid)
        {
            _boardGrid = boardGrid;
        }

        public Vector3 CellToWorld(Vector2Int cell)
        {
            Vector3 localPosition = new(cell.x * _cellSize, 0f, cell.y * _cellSize);
            return GridOrigin.position + GridOrigin.rotation * localPosition;
        }

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            Vector3 localPosition = Quaternion.Inverse(GridOrigin.rotation) * (worldPosition - GridOrigin.position);
            return new Vector2Int(
                Mathf.RoundToInt(localPosition.x / _cellSize),
                Mathf.RoundToInt(localPosition.z / _cellSize));
        }

        private void OnDrawGizmos()
        {
            if (!_areGizmosEnabled || _boardGrid == null)
                return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Transform gridOrigin = GridOrigin;

            Gizmos.matrix = Matrix4x4.TRS(gridOrigin.position, gridOrigin.rotation, Vector3.one);
            Gizmos.color = _gizmoColor;

            float minX = -_cellSize * 0.5f;
            float minZ = -_cellSize * 0.5f;
            float maxX = (_boardGrid.Width - 0.5f) * _cellSize;
            float maxZ = (_boardGrid.Height - 0.5f) * _cellSize;

            for (int x = 0; x <= _boardGrid.Width; x++)
            {
                float lineX = (x - 0.5f) * _cellSize;
                Gizmos.DrawLine(new Vector3(lineX, 0f, minZ), new Vector3(lineX, 0f, maxZ));
            }

            for (int y = 0; y <= _boardGrid.Height; y++)
            {
                float lineZ = (y - 0.5f) * _cellSize;
                Gizmos.DrawLine(new Vector3(minX, 0f, lineZ), new Vector3(maxX, 0f, lineZ));
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
