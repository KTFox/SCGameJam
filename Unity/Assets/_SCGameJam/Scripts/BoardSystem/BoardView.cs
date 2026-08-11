using UnityEngine;

namespace SCJam.BoardSystem
{
    public class BoardView : MonoBehaviour
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private Vector3 _originOffset = Vector3.zero;
        [SerializeField] private Transform _tileContainer;
        [SerializeField] private GameObject _tilePrefab;
        [SerializeField] private GameObject _obstaclePrefab;


        // ===== Private Fields ===== //

        private ParkingBoardData _boardData;


        // ===== Public Properties ===== //

        public float CellSize => _cellSize;


        // ===== Methods ===== //

        public void Initialize(ParkingBoardData boardData)
        {
            _boardData = boardData;
            BuildVisuals();
        }

        public Vector3 CellToWorld(Vector2Int cell)
        {
            return _originOffset + new Vector3(cell.x * _cellSize, 0f, cell.y * _cellSize);
        }

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            Vector3 local = worldPosition - _originOffset;
            int x = Mathf.RoundToInt(local.x / _cellSize);
            int y = Mathf.RoundToInt(local.z / _cellSize);
            return new Vector2Int(x, y);
        }

        private void BuildVisuals()
        {
            if (_boardData == null)
                return;

            ClearVisuals();

            Transform container = _tileContainer != null ? _tileContainer : transform;

            for (int x = 0; x < _boardData.Width; x++)
            {
                for (int y = 0; y < _boardData.Height; y++)
                {
                    Vector2Int cell = new(x, y);
                    GameObject prefab = _boardData.IsCellBlocked(cell) ? _obstaclePrefab : _tilePrefab;
                    if (prefab == null)
                        continue;

                    GameObject instance = Instantiate(prefab, container);
                    instance.transform.position = CellToWorld(cell);
                }
            }
        }

        private void ClearVisuals()
        {
            Transform container = _tileContainer != null ? _tileContainer : transform;

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
        }
    }
}
