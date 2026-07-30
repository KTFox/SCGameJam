using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;



#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Supercent.Rendering.Shadow
{
	[RequireComponent(typeof(Renderer))]
	[DisallowMultipleComponent]
	public class PlanarShadow : MonoBehaviour
	{
#if UNITY_EDITOR
		private static bool _editor_IsValidateWarningDisplayed;
		[SerializeField] private bool _editor_IsPreviewMatAdded = false;
		public bool Editor_UsePivotShadow => _usePivotShadow;
		public bool Editor_IsPivotShadowOffsetDefined => _pivotShadowOffset != Vector3.zero;
#endif
		private const int DEFAULT_INSTANCE_INDEX_VALUE = -1;
		private const string INSTANCE_MAT_NAME = "PlanarShadowInstanceMat";
		private const string SHADER_LIGHT_DIRECTION_PROPERTY = "_LightDirection";
		private static Material _planarShadowOriginalMat = null;
		private static readonly int _setOffsetPropertyID = Shader.PropertyToID("_ShadowPivotOffset");

		[SerializeField] private bool _modifyBoundArea = false;
		public bool ModifyBoundArea => _modifyBoundArea;
		[SerializeField] private Vector3 _boundCenter = Vector3.zero;
		[SerializeField] private Vector3 _boundSize = Vector3.one;
		[SerializeField] private Renderer _renderer = null;
		[SerializeField] private MeshFilter _meshFilter = null;
		[SerializeField] private bool _usePivotShadow = false;
		[SerializeField] private Vector3 _pivotShadowOffset = Vector3.zero;
		[SerializeField] private Material[] _includeMaterials = null, _excludeMaterials = null;
		[SerializeField] private int _instanceMatIndex = DEFAULT_INSTANCE_INDEX_VALUE;
		private Material _instanceMat = null;
		private bool _isShadowEnabled = true;

		private void Awake()
		{
			if (true == _modifyBoundArea)
			{
				ExpandBounds();
			}

			if (true == _usePivotShadow)
			{
				InstantiateMaterial();
			}
			else
			{
				this.enabled = false;
			}
		}

		private void OnDestroy()
		{
			_renderer = null;
			_meshFilter = null;
			_includeMaterials = null;
			_excludeMaterials = null;
			_instanceMat = null;
		}

		void Update()
		{
			RefreshOffset();
		}

		/// <summary>
		/// 그림자를 켜고 끕니다.
		/// </summary>
		/// <param name="isEnable">그림자 활성 여부</param>
		public void ToggleShadow(bool isEnable)
		{
			if (_isShadowEnabled == isEnable)
			{
				return;
			}

			_isShadowEnabled = isEnable;
			_renderer.sharedMaterials = isEnable ? _includeMaterials : _excludeMaterials;

			if (true == isEnable)
			{
				enabled = _usePivotShadow;
			}
			else
			{
				enabled = false;
			}
		}

		private void RefreshOffset()
		{
#if UNITY_EDITOR
			if (null == _instanceMat)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} _instanceMat이 null입니다. 그림자 머티리얼이 올바르게 초기화되지 않았습니다.</color>");
				return;
			}
#endif
			Vector3 nextOffsetValue = _pivotShadowOffset;
			nextOffsetValue.y += transform.position.y;
			_instanceMat.SetVector(_setOffsetPropertyID, nextOffsetValue);
		}

		private void InstantiateMaterial()
		{
			Material[] materials = _renderer.sharedMaterials;
			materials[_instanceMatIndex] = _instanceMat = new Material(GetPlanarShadowOriginalMat());
			_includeMaterials = materials;
			_instanceMat.EnableKeyword("_USE_OFFSET");
			_instanceMat.name = INSTANCE_MAT_NAME;
			_renderer.sharedMaterials = materials;
		}

		private void ExpandBounds()
		{
			if (_meshFilter == null)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} _meshFilter가 null입니다. MeshFilter가 할당되지 않았습니다.</color>");
				return;
			}

			if (_meshFilter.sharedMesh == null)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} _meshFilter.sharedMesh가 null입니다. Mesh가 없습니다.</color>");
				return;
			}

			_meshFilter.mesh.bounds = new Bounds(_boundCenter, _boundSize);
		}

		private static Material GetPlanarShadowOriginalMat()
		{
			if (null == _planarShadowOriginalMat)
			{
				string path = "PlanarShadowMat";
				_planarShadowOriginalMat = Resources.Load<Material>(path);

				if (_planarShadowOriginalMat == null)
					Debug.LogError($"<color=red>[Planar Shadow] Assets/Resources/{path}.mat에 그림자 머티리얼이 없습니다!</color></color>");
			}

			return _planarShadowOriginalMat;
		}
		#region 유니티 에디터 기능
#if UNITY_EDITOR

		#region Validate Shadow Component
		private void OnValidate()
		{
			_renderer = GetComponent<Renderer>();

			if (_renderer is not SkinnedMeshRenderer && _renderer is not MeshRenderer)
			{
				if (_editor_IsValidateWarningDisplayed) return;

				_editor_IsValidateWarningDisplayed = true;

				EditorUtility.DisplayDialog(
					"Planar Shadow 경고",
					$"게임오브젝트 '{name}'에는 MeshRenderer 또는 SkinnedMeshRenderer가 있어야 합니다!\n" +
					"컴포넌트가 삭제됩니다.",
					"확인");

				EditorApplication.delayCall += () =>
				{
					if (this != null)
					{
						Editor_RemovePlanarShadowMaterial();
						DestroyImmediate(this, true);
					}

					_editor_IsValidateWarningDisplayed = false;
				};
			}
		}

		public (bool, string) Editor_ValidateReferences()
		{
			if (_renderer == null)
			{
				if (_meshFilter == null)
				{
					return (this, "Renderer와 MeshFilter 컴포넌트가 등록되지 않았습니다.");
				}
				else
				{
					return (this, "Renderer 컴포넌트가 등록되지 않았습니다.");
				}
			}
			else if (_renderer is MeshRenderer && _meshFilter == null)
			{
				return (this, "MeshRenderer를 사용하지만, MeshFilter 컴포넌트가 등록되지 않았습니다.");
			}
			else if (_renderer is SkinnedMeshRenderer && _meshFilter != null)
			{
				return (this, "SkinnedMeshRenderer를 사용하지만, MeshFilter 컴포넌트가 등록되어 있습니다.");
			}
			else if (!(_renderer is MeshRenderer) && !(_renderer is SkinnedMeshRenderer))
			{
				return (this, $"등록된 Renderer 컴포넌트가 MeshRenderer 또는 SkinnedMeshRenderer가 아닌 {_renderer.GetType()} 입니다.");
			}

			return (false, null);
		}

		public (bool, string) Editor_ValidateDuplicatedComponent()
		{
			var components = GetComponents<PlanarShadow>();
			if (components.Length > 1)
			{
				return (this, "두 개 이상의 PlanarShadow 컴포넌트가 있습니다.");
			}

			return (false, null);
		}

		public (bool, string) Editor_ValidateMaterialSettings()
		{
			if (_renderer == null || _renderer.sharedMaterials == null)
			{
				return (this, "Renderer 또는 sharedMaterials가 null입니다.");
			}

			Material[] materials = _renderer.sharedMaterials;

			if (materials.Length == 1)
			{
				foreach (var material in materials)
				{
					if (material != null && material.shader == GetPlanarShadowOriginalMat().shader)
					{
						return (this, "Renderer에 PlanarShadow 머티리얼이 포함되어 있지만, 머티리얼 배열의 크기가 1입니다.");
					}
				}
			}

			for (int i = 0; i < materials.Length; i++)
			{
				if (materials[i] == null)
				{
					return (this, $"Renderer의 Material 배열에 null이 포함되어 있습니다. 배열 인덱스: {i}");
				}
			}

			foreach (var material in materials)
			{
				if (material != null && material.name.Equals(INSTANCE_MAT_NAME))
				{
					return (this, $"Renderer의 Material 배열에 INSTANCE_MAT_SUFFIX('{INSTANCE_MAT_NAME}') 이름이 포함된 머티리얼이 발견되었습니다.");
				}
			}

			bool hasShadowMaterial = false;

			foreach (var material in materials)
			{
				if (material != null && material.shader == GetPlanarShadowOriginalMat().shader)
				{
					hasShadowMaterial = true;
					break;
				}
			}

			if (!hasShadowMaterial)
			{
				return (this, "Renderer에 PlanarShadow 머티리얼이 포함되어 있지 않습니다.");
			}

			if (_instanceMatIndex == DEFAULT_INSTANCE_INDEX_VALUE)
			{
				return (this, "그림자 머티리얼 인덱싱 설정이 되어 있지 않습니다!");
			}

			if (_instanceMatIndex >= _renderer.sharedMaterials.Length)
			{
				return (this, "그림자 머티리얼 인덱싱 번호가 머티리얼 배열의 범위를 벗어났습니다.");
			}

			if (_renderer.sharedMaterials[_instanceMatIndex].shader != GetPlanarShadowOriginalMat().shader)
			{
				return (this, "그림자 머티리얼 인덱싱 번호에 해당하는 머티리얼이 그림자 머티리얼이 아닙니다!");
			}

			if (_includeMaterials.Length == 0 || _excludeMaterials.Length == 0)
			{
				return (this, "그림자 머티리얼 배열이 에디터에서 캐싱되지 않았습니다.");
			}

			if (_renderer.sharedMaterials.Length != _includeMaterials.Length)
			{
				return (this, "렌더러의 머티리얼 배열 데이터가 에디터에서 캐싱된 머티리얼과 크기가 다릅니다.");
			}

			if (_includeMaterials.Length != _excludeMaterials.Length + 1)
			{
				return (this, "애디터에서 캐싱 된 렌더러의 머티리얼 배열 데이터가 잘못되었습니다.");
			}

			for (int i = 0, length = _renderer.sharedMaterials.Length; i < length; ++i)
			{
				if (null == _renderer.sharedMaterials[i])
					return (this, $"렌더러의 머티리얼 배열에 null 요소가 존재합니다. 인덱스 번호: {i}");

				if (null == _includeMaterials[i])
					return (this, $"에디터에서 캐싱된 머티리얼 중 null 요소가 존재합니다. 인덱스 번호: {i}");

				if (i != length - 1 && null == _excludeMaterials[i])
					return (this, $"에디터에서 캐싱된 머티리얼 중 null 요소가 존재합니다. 인덱스 번호: {i}");

				if (_renderer.sharedMaterials[i] != _includeMaterials[i])
					return (this, $"렌더러의 머티리얼 배열 데이터와 에디터에서 캐싱된 머티리얼의 구성이 다릅니다. 인덱스 번호: {i}");
			}
			return (false, null);
		}

		public (bool, string) Editor_ValidateBounds()
		{
			if (_modifyBoundArea)
			{
				if (_boundSize == Vector3.zero)
				{
					return (this, "Bound 크기(_boundSize)가 설정되지 않았습니다.");
				}

				if (_boundSize.x < 0 || _boundSize.y < 0 || _boundSize.z < 0)
				{
					return (this, $"Bound 크기(_boundSize)가 음수입니다: {_boundSize}. 모든 축 값은 양수여야 합니다.");
				}
			}

			return (false, null);
		}

		public (bool, string) Editor_ValidatePivotShadowConfiguration()
		{
			if (_editor_IsPreviewMatAdded)
			{
				return (this, "피벗 그림자 프리뷰용 머티리얼이 추가되어 있습니다.");
			}

			if (_usePivotShadow && _pivotShadowOffset == Vector3.zero)
			{
				return (this, "Pivot Shadow를 사용하지만 오프셋 값이 설정되지 않았습니다.");
			}

			return (false, null);
		}

		public (bool, string) Editor_ValidateMultiplePivotShadowMaterials()
		{
			if (_renderer != null)
			{
				var shaderCount = _renderer.sharedMaterials
					.Select(mat => mat?.shader)
					.GroupBy(shader => shader)
					.Count(group => group.Key == GetPlanarShadowOriginalMat().shader && group.Count() > 1);

				if (shaderCount > 0)
				{
					return (true, "평면 그림자 머티리얼이 두 개 이상 존재합니다.");
				}
			}

			return (false, null);
		}

		#endregion

		#region 그림자 제어 기능
		private void Reset()
		{
			Editor_ResetSettings();
			EditorUtility.SetDirty(gameObject);
		}

		public void Editor_CacheMaterialsArray()
		{
			if (_renderer == null)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} _renderer가 null입니다. Renderer가 설정되어 있는지 확인하세요.</color>");
				return;
			}

			_includeMaterials = _renderer.sharedMaterials;
			if (_includeMaterials == null)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} _renderer.sharedMaterials가 null입니다.</color>");
				return;
			}

			int excludeMatLength = _includeMaterials.Length - 1;

			if (excludeMatLength < 0)
			{
				Debug.LogWarning($"<color=red>[Planar Shadow] {name} _includeMaterials 배열 길이가 1보다 작습니다.</color>");
			}

			_excludeMaterials = new Material[excludeMatLength];
			int index = 0;

			if (DEFAULT_INSTANCE_INDEX_VALUE == _instanceMatIndex)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} 그림자 머티리얼 인덱스 번호가 초기화되지 않았습니다!</color>");
				return;
			}

			for (int i = 0; i < _includeMaterials.Length; ++i)
			{
				if (i != _instanceMatIndex)
				{
					_excludeMaterials[index++] = _includeMaterials[i];
				}
			}
		}

		private void Editor_PreviewPivotOffset()
		{
			if (true == Application.isPlaying)
				return;

			Material[] materials = _renderer.sharedMaterials;
			if (materials == null || materials.Length == 0)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} _renderer.sharedMaterials가 null이거나 비어 있습니다.</color>");
				return;
			}

			for (int i = materials.Length - 1; i >= 0; --i)
				if (materials[i] != null && materials[i].shader == GetPlanarShadowOriginalMat().shader)
				{
					Vector3 nextOffsetValue = _pivotShadowOffset;
					nextOffsetValue.y += transform.position.y;
					materials[i].SetVector(_setOffsetPropertyID, nextOffsetValue);
					return;
				}
		}

		private void Editor_CalculatePivotShadowOffset()
		{
			Mesh mesh;

			if (_meshFilter != null && _meshFilter.sharedMesh != null)
			{
				mesh = _meshFilter.sharedMesh;
			}
			else if (_renderer is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh != null)
			{
				mesh = skinnedMeshRenderer.sharedMesh;
			}
			else
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} MeshFilter 또는 SkinnedMeshRenderer가 할당되지 않았습니다.</color>");
				return;
			}

			if (mesh == null || mesh.vertices == null || mesh.vertices.Length == 0)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} 유효한 Mesh 또는 정점 데이터가 없습니다.</color>");
				return;
			}

			Vector3[] vertices = mesh.vertices;
			Matrix4x4 localToWorldMatrix = _renderer.transform.localToWorldMatrix;

			float lowestY = float.MaxValue;

			for (int i = 0; i < vertices.Length; i++)
			{
				Vector3 worldPosition = localToWorldMatrix.MultiplyPoint3x4(vertices[i]);

				if (worldPosition.y < lowestY)
					lowestY = worldPosition.y;
			}

			_pivotShadowOffset = new Vector3(0f, lowestY - transform.position.y, 0f);
		}

		public void Editor_AddPlanarShadowMaterial()
		{
			if (false == Editor_RefreshMaterialArray())
				return;

			if (_renderer == null)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} Renderer가 null입니다. Renderer가 설정되어 있는지 확인하세요.</color>");
				return;
			}

			Material planarShadowOriginalMat = GetPlanarShadowOriginalMat();

			if (planarShadowOriginalMat == null)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} PlanarShadowOriginalMat 초기화 실패.</color>");
				return;
			}

			var currentMaterials = _renderer.sharedMaterials;
			if (currentMaterials == null || currentMaterials.Length == 0)
			{
				Debug.LogWarning($"<color=red>[Planar Shadow] {name} 현재 Material 배열이 null이거나 비어 있습니다.</color>");
				return;
			}

			int idx = 0;
			foreach (var material in currentMaterials)
			{
				if (material == planarShadowOriginalMat)
				{
					_instanceMatIndex = idx;
					return;
				}

				++idx;
			}

			var newMaterials = new Material[currentMaterials.Length + 1];
			Array.Copy(currentMaterials, 0, newMaterials, 0, currentMaterials.Length);
			newMaterials[newMaterials.Length - 1] = planarShadowOriginalMat;
			_instanceMatIndex = currentMaterials.Length;
			_renderer.sharedMaterials = newMaterials;

			Editor_TogglePivotRenderingPreview(_usePivotShadow);
		}

		public void Editor_RemovePlanarShadowMaterial()
		{
			if (false == Editor_RefreshMaterialArray())
				return;

			_renderer.sharedMaterials = _renderer.sharedMaterials
				.Where(mat => mat.shader != GetPlanarShadowOriginalMat().shader)
				.ToArray();
		}

		public bool Editor_RefreshMaterialArray()
		{
			if (_renderer == null)
			{
				Debug.LogWarning($"<color=red>[Planar Shadow] {name} Renderer가 null입니다!</color>");
				return false;
			}

			if (_renderer.sharedMaterials.Any(mat => mat == null))
			{
				Selection.activeGameObject = _renderer.gameObject;
				EditorGUIUtility.PingObject(_renderer.gameObject);

				bool removeNullMaterials = EditorUtility.DisplayDialog(
					"Null 머티리얼 제거 옵션",
					$"{name}의 Renderer에 null인 머티리얼이 포함되어 있습니다.\n" +
					"null 머티리얼도 함께 제거하시겠습니까?",
					"예 (null 제거)",
					"아니오 (null 유지, 오류 발생 가능)"
				);

				if (removeNullMaterials)
				{
					_renderer.sharedMaterials = _renderer.sharedMaterials
						.Where(mat => mat != null && mat.shader != GetPlanarShadowOriginalMat().shader)
						.ToArray();

					return true;
				}
				else
				{
					_renderer.sharedMaterials = _renderer.sharedMaterials
						.Where(mat => mat == null || mat.shader != GetPlanarShadowOriginalMat().shader)
						.ToArray();

					return false;
				}
			}

			return true;
		}

		public void Editor_FindComponents()
		{
			_renderer = GetComponent<Renderer>();
			_meshFilter = GetComponent<MeshFilter>();

			if (_renderer == null)
				Debug.LogWarning($"<color=red>[Planar Shadow] {name} Renderer가 할당되지 않았습니다.</color>");

			if (_renderer == null && _meshFilter == null)
				Debug.LogWarning($"<color=red>[Planar Shadow] {name} MeshFilter가 할당되지 않았습니다.</color>");
		}

		public void Editor_CalculateBoundArea()
		{
			if (_meshFilter == null || _meshFilter.sharedMesh == null)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} MeshFilter 또는 공유 Mesh가 null입니다.</color>");
				return;
			}

			Mesh mesh = _meshFilter.sharedMesh;
			Vector3[] vertices = mesh.vertices;

			if (vertices == null || vertices.Length == 0)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} Mesh의 정점 데이터가 유효하지 않습니다.</color>");
				return;
			}

			Material mat = GetPlanarShadowOriginalMat();
			if (mat == null)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} PlanarShadowOriginalMat이 null입니다.</color>");
				return;
			}

			Vector3 lightDirection = mat.GetVector(SHADER_LIGHT_DIRECTION_PROPERTY);
			Vector3 shadowPivotOffset = Editor_GetShadowPivotOffsetForProjection(mat);

			Vector3 minBound = Vector3.positiveInfinity;
			Vector3 maxBound = Vector3.negativeInfinity;

			for (int i = 0; i < vertices.Length; i++)
			{
				Vector3 localPos = vertices[i];

				Vector3 worldPos = transform.TransformPoint(localPos);
				Vector3 shadowPos = Editor_ProjectWorldPositionToShadowPlane(worldPos, lightDirection, shadowPivotOffset);
				Vector3 shadowLocalPos = transform.InverseTransformPoint(shadowPos);

				minBound = Vector3.Min(minBound, localPos);
				minBound = Vector3.Min(minBound, shadowLocalPos);
				maxBound = Vector3.Max(maxBound, localPos);
				maxBound = Vector3.Max(maxBound, shadowLocalPos);
			}

			Vector3 boundCenter = (minBound + maxBound) / 2;
			Vector3 boundSize = maxBound - minBound;

			_boundCenter = boundCenter;
			_boundSize = boundSize;

			EditorUtility.SetDirty(this);
		}

		public void Editor_TogglePivotRenderingMode(bool isEnable)
		{
			Editor_TogglePivotRenderingPreview(false);

			if (true == isEnable)
			{
				_usePivotShadow = true;
			}
			else
			{
				_usePivotShadow = false;
				_editor_IsPreviewMatAdded = false;
			}
		}

		public void Editor_TogglePivotRenderingPreview(bool isEnabled)
		{
			Editor_RefreshMaterialArray();

			Material originalMat = GetPlanarShadowOriginalMat();
			if (originalMat == null)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} PlanarShadowOriginalMat이 null입니다.</color>");
				return;
			}

			Material[] materials = _renderer.sharedMaterials;
			if (materials == null || materials.Length == 0)
			{
				Debug.LogWarning($"<color=red>[Planar Shadow] {name} Renderer의 Materials 배열이 null이거나 비어 있습니다.</color>");
				return;
			}

			if (isEnabled)
			{
				for (int i = 0; i < materials.Length; i++)
				{
					if (materials[i] == originalMat)
					{
						_instanceMat = new Material(originalMat);
						_instanceMat.EnableKeyword("_USE_OFFSET");
						_instanceMat.name = INSTANCE_MAT_NAME;

						materials[i] = _instanceMat;

						_instanceMatIndex = i;

						_editor_IsPreviewMatAdded = true;
						break;
					}
				}
			}
			else
			{
				for (int i = 0; i < materials.Length; i++)
				{
					if (materials[i].name.Equals(INSTANCE_MAT_NAME))
					{
						_instanceMat = null;

						materials[i] = originalMat;

						_instanceMatIndex = i;

						_editor_IsPreviewMatAdded = false;
						break;
					}
				}
			}

			_renderer.sharedMaterials = materials;

			EditorUtility.SetDirty(this);
		}

		private void Editor_RefreshPrefabMaterial()
		{
			if (null == _renderer)
			{
				Debug.LogWarning($"<color=red>[Planar Shadow] {name} Renderer가 null입니다!</color>");
				return;
			}

			Material[] materials = _renderer.sharedMaterials;
			if (materials == null || materials.Length == 0)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} Renderer의 Materials 배열이 null이거나 비어 있습니다.</color>");
				return;
			}

			for (int i = materials.Length - 1; i >= 0; --i)
			{
				if (null == materials[i] && i == _instanceMatIndex)
				{
					materials[i] = GetPlanarShadowOriginalMat();
					_renderer.sharedMaterials = materials;

					Editor_TogglePivotRenderingPreview(_usePivotShadow);

					return;
				}
			}
		}

		public void Editor_ResetSettings()
		{
			Editor_FindComponents();
			Editor_RemovePlanarShadowMaterial();
			Editor_AddPlanarShadowMaterial();
			Editor_TogglePivotRenderingPreview(false);
			Editor_CacheMaterialsArray();

			if (ModifyBoundArea)
				Editor_CalculateBoundArea();
		}
		#endregion

		#region Bake Shadow
		private static Material Editor_GetPlanarShadowBakedMat()
		{
			string path = "PlanarShadowBakedMat";
			Material planarShadowOriginalMat = Resources.Load<Material>(path);

			if (planarShadowOriginalMat == null)
				Debug.LogError($"<color=red>[Planar Shadow] Assets/Resources/{path}.mat에 그림자 머티리얼이 없습니다!</color>");

			return planarShadowOriginalMat;
		}

		private Vector3 Editor_GetShadowPivotOffsetForProjection(Material mat)
		{
			if (true == _usePivotShadow)
			{
				Vector3 offset = _pivotShadowOffset;
				offset.y += transform.position.y;
				return offset;
			}

			return mat.GetVector(_setOffsetPropertyID);
		}

		private Vector3 Editor_ProjectWorldPositionToShadowPlane(Vector3 worldPosition, Vector3 lightDirection, Vector3 shadowPivotOffset)
		{
			Vector3 shadowPosition = worldPosition + lightDirection * (worldPosition.y - shadowPivotOffset.y);
			shadowPosition.y = shadowPivotOffset.y;

			if (true == _usePivotShadow)
			{
				shadowPosition.x += shadowPivotOffset.x;
				shadowPosition.z += shadowPivotOffset.z;
			}

			return shadowPosition;
		}

		private void Editor_BakeShadowOptimized()
		{
			string timestamp = System.DateTime.Now.ToString("yyMMddHHmmss");
			string fileName = $"{_meshFilter.sharedMesh.name}_optimized_{timestamp}.asset";
			string assetPath = System.IO.Path.Combine("Assets", fileName);

			Mesh mesh = _meshFilter.sharedMesh;
			Vector3[] vertices = mesh.vertices;

			Material mat = GetPlanarShadowOriginalMat();
			if (mat == null)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} PlanarShadowOriginalMat이 null입니다.</color>");
				return;
			}

			Vector3 lightDirection = mat.GetVector(SHADER_LIGHT_DIRECTION_PROPERTY);
			Vector3 shadowPivotOffset = Editor_GetShadowPivotOffsetForProjection(mat);

			HashSet<Vector3> uniquePositions = new HashSet<Vector3>();

			for (int i = 0; i < vertices.Length; i++)
			{
				Vector3 localPos = vertices[i];
				Vector3 worldPos = transform.TransformPoint(localPos);
				Vector3 shadowPos = Editor_ProjectWorldPositionToShadowPlane(worldPos, lightDirection, shadowPivotOffset);
				Vector3 localShadowPos = transform.InverseTransformPoint(shadowPos);
				uniquePositions.Add(localShadowPos);
			}

			List<Vector3> outlinePoints = Editor_ExtractConvexHull(uniquePositions);
			Mesh generatedMesh = Editor_CreateOutlineMesh(outlinePoints.ToArray());

			AssetDatabase.CreateAsset(generatedMesh, assetPath);
			AssetDatabase.SaveAssets();
			Debug.Log($"<color=cyan>[Planar Shadow] 메쉬 파일이 {assetPath}에 생성되었습니다.");

			Editor_ApplyBakedMesh(generatedMesh, "Optimized");
		}

		private void Editor_BakeShadowFull()
		{
			string timestamp = System.DateTime.Now.ToString("yyMMddHHmmss");
			string fileName = $"{_meshFilter.sharedMesh.name}_full_{timestamp}.asset";
			string assetPath = System.IO.Path.Combine("Assets", fileName);

			Mesh originalMesh = _meshFilter.sharedMesh;
			Vector3[] originalVertices = originalMesh.vertices;
			int[] originalTriangles = originalMesh.triangles;

			Material mat = GetPlanarShadowOriginalMat();
			if (mat == null)
			{
				Debug.LogError($"<color=red>[Planar Shadow] {name} PlanarShadowOriginalMat이 null입니다.</color>");
				return;
			}

			Vector3 lightDirection = mat.GetVector(SHADER_LIGHT_DIRECTION_PROPERTY);
			Vector3 shadowPivotOffset = Editor_GetShadowPivotOffsetForProjection(mat);

			List<Vector3> shadowVertices = new List<Vector3>();
			Dictionary<int, int> indexMap = new Dictionary<int, int>();

			for (int i = 0; i < originalVertices.Length; i++)
			{
				Vector3 worldPos = transform.TransformPoint(originalVertices[i]);
				Vector3 shadowPos = Editor_ProjectWorldPositionToShadowPlane(worldPos, lightDirection, shadowPivotOffset);
				Vector3 localShadowPos = transform.InverseTransformPoint(shadowPos);

				if (!shadowVertices.Contains(localShadowPos))
				{
					indexMap[i] = shadowVertices.Count;
					shadowVertices.Add(localShadowPos);
				}
				else
				{
					indexMap[i] = shadowVertices.IndexOf(localShadowPos);
				}
			}

			List<int> shadowTriangles = new List<int>();
			for (int i = 0; i < originalTriangles.Length; i += 3)
			{
				int v0 = indexMap[originalTriangles[i]];
				int v1 = indexMap[originalTriangles[i + 1]];
				int v2 = indexMap[originalTriangles[i + 2]];

				shadowTriangles.Add(v0);
				shadowTriangles.Add(v1);
				shadowTriangles.Add(v2);
			}

			Mesh shadowMesh = new Mesh();
			shadowMesh.vertices = shadowVertices.ToArray();
			shadowMesh.triangles = shadowTriangles.ToArray();

			AssetDatabase.CreateAsset(shadowMesh, assetPath);
			AssetDatabase.SaveAssets();
			Debug.Log($"<color=cyan>[Planar Shadow] 메쉬 파일이 {assetPath}에 생성되었습니다.");

			Editor_ApplyBakedMesh(shadowMesh, "Full");
		}

		private void Editor_ApplyBakedMesh(Mesh mesh, string type)
		{
			GameObject outlineObject = new GameObject($"{name}_baked_planar_shadow_{type.ToLower()}");
			outlineObject.transform.SetParent(transform, false);
			outlineObject.transform.localPosition = Vector3.zero;
			outlineObject.transform.localRotation = Quaternion.identity;
			outlineObject.transform.localScale = Vector3.one;

			MeshFilter meshFilter = outlineObject.AddComponent<MeshFilter>();
			meshFilter.mesh = mesh;

			Material planarShadowMat = Editor_GetPlanarShadowBakedMat();
			MeshRenderer meshRenderer = outlineObject.AddComponent<MeshRenderer>();
			meshRenderer.material = planarShadowMat;
		}

		private static List<Vector3> Editor_ExtractConvexHull(HashSet<Vector3> points)
		{
			List<Vector3> pointList = new List<Vector3>(points);
			pointList.Sort((a, b) => a.x.CompareTo(b.x));

			List<Vector3> hull = new List<Vector3>();

			foreach (var p in pointList)
			{
				while (hull.Count >= 2 && Editor_Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0)
					hull.RemoveAt(hull.Count - 1);
				hull.Add(p);
			}

			int t = hull.Count + 1;
			for (int i = pointList.Count - 1; i >= 0; i--)
			{
				Vector3 p = pointList[i];
				while (hull.Count >= t && Editor_Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0)
					hull.RemoveAt(hull.Count - 1);
				hull.Add(p);
			}

			hull.RemoveAt(hull.Count - 1);
			return hull;
		}

		private static float Editor_Cross(Vector3 o, Vector3 a, Vector3 b)
		{
			return (a.x - o.x) * (b.z - o.z) - (a.z - o.z) * (b.x - o.x);
		}

		private static Mesh Editor_CreateOutlineMesh(Vector3[] outlinePoints)
		{
			Mesh mesh = new Mesh();

			Vector3[] vertices = outlinePoints;
			int[] triangles = new int[(vertices.Length - 2) * 3];

			for (int i = 1; i < vertices.Length - 1; i++)
			{
				triangles[(i - 1) * 3] = 0;
				triangles[(i - 1) * 3 + 1] = i + 1;
				triangles[(i - 1) * 3 + 2] = i;
			}

			mesh.vertices = vertices;
			mesh.triangles = triangles;
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();

			return mesh;
		}
		#endregion // Bake Shadow

		#region Inspector Editor
		[CustomEditor(typeof(PlanarShadow)), CanEditMultipleObjects]
		public class PlanarShadowInspectorEditor : Editor
		{
			private readonly List<PlanarShadow> _selectedPlanarShadows = new List<PlanarShadow>();
			private PlanarShadow _baseTarget = null;
			private SerializedProperty _renderer;
			private SerializedProperty _meshFilter;
			private SerializedProperty _pivotShadowOffset;

			private SkinnedMeshRenderer _skinnedMeshRenderer;
			private MeshRenderer _meshRenderer;
			private bool _previousUsePivotShadow;
			private bool _isPivotPreviewMatAdded;


			private void OnEnable()
			{
				_baseTarget = (PlanarShadow)target;

				_renderer = serializedObject.FindProperty("_renderer");
				_meshFilter = serializedObject.FindProperty("_meshFilter");
				_pivotShadowOffset = serializedObject.FindProperty("_pivotShadowOffset");

				_skinnedMeshRenderer = _baseTarget.GetComponent<SkinnedMeshRenderer>();
				_meshRenderer = _baseTarget.GetComponent<MeshRenderer>();
				_previousUsePivotShadow = _baseTarget._usePivotShadow;
				_isPivotPreviewMatAdded = _baseTarget._editor_IsPreviewMatAdded;
			}

			private void ModifyingPivotOffset()
			{
				List<SerializedProperty> targetOffsets = new List<SerializedProperty>();

				foreach (PlanarShadow planarShadow in _selectedPlanarShadows)
				{
					if (planarShadow._usePivotShadow)
					{
						SerializedObject serializedShadow = new SerializedObject(planarShadow);
						SerializedProperty shadowOffset = serializedShadow.FindProperty("_pivotShadowOffset");
						targetOffsets.Add(shadowOffset);
					}
				}

				if (targetOffsets.Count == 0)
				{
					EditorGUILayout.HelpBox("None of the selected objects use _usePivotShadow.", MessageType.Warning);
					return;
				}

				Vector3 pivotShadowOffset = targetOffsets[0].vector3Value;

				EditorGUI.BeginChangeCheck();
				pivotShadowOffset = EditorGUILayout.Vector3Field("Shadow Pivot Offset", pivotShadowOffset);

				if (EditorGUI.EndChangeCheck())
				{
					foreach (SerializedProperty targetOffset in targetOffsets)
					{
						targetOffset.vector3Value = pivotShadowOffset;
						targetOffset.serializedObject.ApplyModifiedProperties();
					}
				}

				EditorGUILayout.BeginHorizontal();

				if (GUILayout.Button("Copy Values", GUILayout.Height(25)))
				{
					EditorGUIUtility.systemCopyBuffer = $"{pivotShadowOffset.x},{pivotShadowOffset.y},{pivotShadowOffset.z}";
				}

				if (GUILayout.Button("Paste Values", GUILayout.Height(25)))
				{
					string[] values = EditorGUIUtility.systemCopyBuffer.Split(',');
					if (values.Length == 3 &&
						float.TryParse(values[0], out float x) &&
						float.TryParse(values[1], out float y) &&
						float.TryParse(values[2], out float z))
					{
						pivotShadowOffset = new Vector3(x, y, z);
						foreach (SerializedProperty targetOffset in targetOffsets)
						{
							targetOffset.vector3Value = pivotShadowOffset;
							targetOffset.serializedObject.ApplyModifiedProperties();
						}
					}
				}

				EditorGUILayout.EndHorizontal();

				if (GUILayout.Button("Auto-Calculate Offset", GUILayout.Height(25)))
				{
					foreach (PlanarShadow planarShadow in _selectedPlanarShadows)
					{
						if (planarShadow._usePivotShadow)
						{
							planarShadow.Editor_CalculatePivotShadowOffset();
							EditorUtility.SetDirty(planarShadow);
						}
					}
				}
			}

			public override void OnInspectorGUI()
			{
				if (!_baseTarget.enabled)
					return;

				_selectedPlanarShadows.Clear();
				serializedObject.Update();
				_baseTarget.Editor_RefreshPrefabMaterial();

				#region Get Selected Shadow Components
				foreach (GameObject selection in Selection.gameObjects)
					if (selection.TryGetComponent(out PlanarShadow planarShadow))
						_selectedPlanarShadows.Add(planarShadow);
				#endregion

				#region Component Fields
				EditorGUILayout.Space(10);

				EditorGUILayout.BeginVertical("box");
				EditorGUILayout.PropertyField(_renderer, GUIContent.none);
				if (false == _skinnedMeshRenderer)
					EditorGUILayout.PropertyField(_meshFilter, GUIContent.none);
				#endregion

				#region Refresh Button
				if (GUILayout.Button("Stabilize Shadow Settings", GUILayout.Height(25)))
				{
					foreach (PlanarShadow planarShadow in _selectedPlanarShadows)
					{
						planarShadow.Editor_ResetSettings();
						EditorUtility.SetDirty(planarShadow);
					}
				}
				EditorGUILayout.EndVertical();
				#endregion

				#region Pivot Shadow Feature
				EditorGUILayout.Space(10);
				EditorGUILayout.BeginVertical("box");

				if (true == Application.isPlaying)
				{
					EditorGUILayout.HelpBox("Pivot shadow cannot be toggled in Play mode.", MessageType.Info);
				}
				else
				{
					EditorGUILayout.BeginHorizontal();
					GUI.enabled = !_previousUsePivotShadow;
					if (GUILayout.Button("Enable Pivot Shadow Rendering", GUILayout.Height(25)))
					{
						foreach (PlanarShadow planarShadow in _selectedPlanarShadows)
						{
							planarShadow.Editor_TogglePivotRenderingMode(true);
							_previousUsePivotShadow = true;
							EditorUtility.SetDirty(planarShadow);
						}
					}

					GUI.enabled = _previousUsePivotShadow;
					if (GUILayout.Button("Disable Pivot Shadow Rendering", GUILayout.Height(25)))
					{
						foreach (PlanarShadow planarShadow in _selectedPlanarShadows)
						{
							planarShadow.Editor_TogglePivotRenderingMode(false);
							_previousUsePivotShadow = false;
							EditorUtility.SetDirty(planarShadow);
						}
					}
					GUI.enabled = true;
					EditorGUILayout.EndHorizontal();
				}

				if (true == _baseTarget._usePivotShadow)
				{
					if (false == Application.isPlaying)
					{
						EditorGUILayout.BeginHorizontal();

						GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
						buttonStyle.fixedHeight = 25;

						if (_isPivotPreviewMatAdded)
						{
							GUI.backgroundColor = Color.red;
							if (GUILayout.Button("Disable Pivot Shadow Preview", buttonStyle))
							{
								foreach (PlanarShadow planarShadow in _selectedPlanarShadows)
								{
									if (true == planarShadow._usePivotShadow)
									{
										_isPivotPreviewMatAdded = false;
										planarShadow.Editor_TogglePivotRenderingPreview(false);
										EditorUtility.SetDirty(planarShadow);
									}
								}
							}
							GUI.backgroundColor = Color.white;
							EditorGUILayout.EndHorizontal();

						}
						else
						{
							GUI.backgroundColor = Color.green;
							if (GUILayout.Button("Enable Pivot Shadow Preview", buttonStyle))
							{
								foreach (PlanarShadow planarShadow in _selectedPlanarShadows)
								{
									if (true == planarShadow._usePivotShadow)
									{
										_isPivotPreviewMatAdded = true;
										planarShadow.Editor_TogglePivotRenderingPreview(true);
										EditorUtility.SetDirty(planarShadow);
									}
								}
							}

							GUI.backgroundColor = Color.white;
							EditorGUILayout.EndHorizontal();

							if (false == _isPivotPreviewMatAdded)
							{
								EditorGUILayout.HelpBox("Enable pivot shadow preview to edit values.", MessageType.Info);

								EditorGUILayout.Space(10);

								GUI.enabled = false;
								EditorGUILayout.Vector3Field("Shadow Pivot Offset", _pivotShadowOffset.vector3Value);
								GUI.enabled = true;
							}
						}
					}

					if (true == Application.isPlaying)
					{
						if (true == _baseTarget._usePivotShadow)
						{
							ModifyingPivotOffset();
						}
					}
					else
					{
						if (true == _baseTarget._usePivotShadow && true == _baseTarget._editor_IsPreviewMatAdded)
						{
							EditorGUILayout.HelpBox("A preview material is assigned to the current object. When finished, press [Disable Pivot Shadow Preview] to restore.", MessageType.Error);

							EditorGUILayout.Space(10);

							ModifyingPivotOffset();
						}
					}

					foreach (PlanarShadow planarShadow in _selectedPlanarShadows)
						if (true == planarShadow._usePivotShadow && true == planarShadow._editor_IsPreviewMatAdded)
							planarShadow.Editor_PreviewPivotOffset();
				}
				EditorGUILayout.EndVertical();
				#endregion

				#region Bound Area Adjustment

				if (null == _skinnedMeshRenderer && null != _meshRenderer)
				{
					EditorGUILayout.Space(10);
					EditorGUILayout.BeginVertical("box");
					EditorGUILayout.BeginHorizontal();

					GUI.enabled = !_baseTarget._modifyBoundArea;
					if (GUILayout.Button("Enable Bound Area Adjustment", GUILayout.Height(25)))
					{
						foreach (PlanarShadow planarShadow in _selectedPlanarShadows)
						{
							if (planarShadow._renderer is MeshRenderer)
							{
								planarShadow._modifyBoundArea = true;
								EditorUtility.SetDirty(planarShadow);
							}
						}
					}

					GUI.enabled = _baseTarget._modifyBoundArea;
					if (GUILayout.Button("Disable Bound Area Adjustment", GUILayout.Height(25)))
					{
						foreach (PlanarShadow planarShadow in _selectedPlanarShadows)
						{
							if (planarShadow._renderer is MeshRenderer)
							{
								planarShadow._modifyBoundArea = false;
								EditorUtility.SetDirty(planarShadow);
							}
						}
					}

					GUI.enabled = true;
					EditorGUILayout.EndHorizontal();

					if (_baseTarget._modifyBoundArea)
					{
						EditorGUILayout.HelpBox("Adjusting the bound area can fix shadows disappearing abruptly. Increasing the render range may affect performance.", MessageType.Warning);
						if (GUILayout.Button("Calculate Bound Area", GUILayout.Height(25)))
						{
							foreach (PlanarShadow planarShadow in _selectedPlanarShadows)
							{
								if (planarShadow._modifyBoundArea)
								{
									planarShadow.Editor_CalculateBoundArea();
									EditorUtility.SetDirty(planarShadow);
								}
							}
						}
					}

					EditorGUILayout.EndVertical();
				}

				#endregion

				#region Remove Shadow
				EditorGUILayout.Space(10);

				EditorGUILayout.BeginVertical("box");

				GUI.backgroundColor = Color.red;
				if (GUILayout.Button("Remove Shadow", GUILayout.Height(25)))
				{
					List<PlanarShadow> removedShadows = new List<PlanarShadow>();
					List<GameObject> removedShadowGos = new List<GameObject>();

					foreach (PlanarShadow planarShadow in _selectedPlanarShadows)
					{
						planarShadow.Editor_RemovePlanarShadowMaterial();
						removedShadows.Add(planarShadow);
						removedShadowGos.Add(planarShadow.gameObject);
					}

					foreach (PlanarShadow shadow in removedShadows)
					{
						GameObject.DestroyImmediate(shadow, true);
					}

					foreach (GameObject shadowGo in removedShadowGos)
					{
						EditorUtility.SetDirty(shadowGo);
					}

					AssetDatabase.SaveAssets();
					AssetDatabase.Refresh();
				}

				GUI.backgroundColor = Color.white;
				EditorGUILayout.EndVertical();
				#endregion

				#region Bake Shadow
				EditorGUILayout.BeginVertical("box");
				EditorGUILayout.BeginHorizontal();

				if (GUILayout.Button("Bake Shadow (Simplified)", GUILayout.Height(25)))
				{
					SceneView.lastActiveSceneView.LookAt(_baseTarget.transform.position);
					_baseTarget.Editor_BakeShadowOptimized();
					_baseTarget.Editor_RemovePlanarShadowMaterial();
					EditorUtility.SetDirty(_baseTarget);
					DestroyImmediate(_baseTarget);
				}

				if (GUILayout.Button("Bake Shadow (Original)", GUILayout.Height(25)))
				{
					SceneView.lastActiveSceneView.LookAt(_baseTarget.transform.position);
					_baseTarget.Editor_BakeShadowFull();
					_baseTarget.Editor_RemovePlanarShadowMaterial();
					EditorUtility.SetDirty(_baseTarget);
					DestroyImmediate(_baseTarget);
				}

				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();
				#endregion

				#region Guide
				EditorGUILayout.BeginVertical("box");
				GUI.backgroundColor = Color.cyan;
				if (GUILayout.Button("Open Guide", GUILayout.Height(25)))
				{
					Application.OpenURL("https://www.notion.so/supercent/10a93b2d25738022a4b6f6edf615781c");
				}
				GUI.backgroundColor = Color.white;
				EditorGUILayout.EndVertical();
				#endregion

				EditorGUILayout.Space(5);
			}
		}
		#endregion
#endif
		#endregion // 유니티 에디터 기능
	}
}
