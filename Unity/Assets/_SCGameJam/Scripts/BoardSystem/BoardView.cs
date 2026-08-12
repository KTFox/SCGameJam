using UnityEngine;

namespace SCJam.BoardSystem
{
    public class BoardView : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField, Tooltip("Transform at the center of the entire board grid.")]
        private Transform _gridOrigin;
        [SerializeField, Min(0.1f)] private float _cellSize;
        [SerializeField] private bool _areGizmosEnabled = true;
        [SerializeField] private Color _gizmoColor;
        [SerializeField] private Color _occupiedCellGizmoColor;


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
            Vector3 localPosition = GetCellLocalPosition(cell);
            return GridOrigin.position + GridOrigin.rotation * localPosition;
        }

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            Vector3 localPosition = Quaternion.Inverse(GridOrigin.rotation) * (worldPosition - GridOrigin.position);
            float firstCellCenterX = (_boardGrid.Width - 1) * _cellSize * -0.5f;
            float firstCellCenterZ = (_boardGrid.Height - 1) * _cellSize * -0.5f;

            return new Vector2Int(
                Mathf.RoundToInt((localPosition.x - firstCellCenterX) / _cellSize),
                Mathf.RoundToInt((localPosition.z - firstCellCenterZ) / _cellSize));
        }

        private Vector3 GetCellLocalPosition(Vector2Int cell)
        {
            float x = (cell.x - (_boardGrid.Width - 1) * 0.5f) * _cellSize;
            float z = (cell.y - (_boardGrid.Height - 1) * 0.5f) * _cellSize;
            return new Vector3(x, 0f, z);
        }


        // ===== Gizmos ===== //

        private void OnDrawGizmos()
        {
            if (!_areGizmosEnabled || _boardGrid == null)
                return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Transform gridOrigin = GridOrigin;

            Gizmos.matrix = Matrix4x4.TRS(gridOrigin.position, gridOrigin.rotation, Vector3.one);

            DrawOccupiedCells();
            DrawGrid();

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private void DrawOccupiedCells()
        {
            Gizmos.color = _occupiedCellGizmoColor;

            for (int x = 0; x < _boardGrid.Width; x++)
            {
                for (int y = 0; y < _boardGrid.Height; y++)
                {
                    if (!_boardGrid.IsCellOccupied(new Vector2Int(x, y)))
                        continue;

                    Vector3 cellCenter = GetCellLocalPosition(new Vector2Int(x, y));
                    Gizmos.DrawCube(cellCenter, new Vector3(_cellSize, 0.02f, _cellSize));
                }
            }
        }

        private void DrawGrid()
        {
            Gizmos.color = _gizmoColor;

            float minX = _boardGrid.Width * _cellSize * -0.5f;
            float minZ = _boardGrid.Height * _cellSize * -0.5f;
            float maxX = -minX;
            float maxZ = -minZ;

            for (int x = 0; x <= _boardGrid.Width; x++)
            {
                float lineX = minX + x * _cellSize;
                Gizmos.DrawLine(new Vector3(lineX, 0f, minZ), new Vector3(lineX, 0f, maxZ));
            }

            for (int y = 0; y <= _boardGrid.Height; y++)
            {
                float lineZ = minZ + y * _cellSize;
                Gizmos.DrawLine(new Vector3(minX, 0f, lineZ), new Vector3(maxX, 0f, lineZ));
            }
        }
    }
}
