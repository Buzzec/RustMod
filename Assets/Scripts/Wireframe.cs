using System.Collections.Generic;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Inventory;
using Assets.Scripts.Util;
using UnityEngine;

namespace Assets.Scripts.UI
{
	public class Wireframe : MonoBehaviour
	{
		public List<Edge> WireframeEdges = new List<Edge>();

		public Bounds BlueprintBounds;

		public Transform BlueprintTransform;

		public MeshFilter BlueprintMeshFilter;

		public Renderer BlueprintRenderer;

		public Material LineMaterial;

		public bool ShowTransformArrow = true;

		public static Color ColorBlueAlpha = Color.blue.SetAlpha(0.3f);

		private bool _redraw;

		private Grid3 _lastGrid;

		private Grid3 _currentGrid;

		private Vector3 _lastPosition;

		private Vector3 _currentPosition;

		private Quaternion _lastRotation;

		private Quaternion _currentRotation;

		private Vector3 TransformPoint(Vector3 point)
		{
			return BlueprintTransform.position + BlueprintTransform.rotation * BlueprintBounds.center + InputHelpers.RotatePointAroundPivot(point, Vector3.zero, BlueprintTransform.rotation.eulerAngles);
		}

		private void CreateLineMaterial()
		{
			if (!LineMaterial)
			{
				Shader shader = Shader.Find("Hidden/Internal-Colored");
				LineMaterial = new Material(shader);
				LineMaterial.hideFlags = HideFlags.HideAndDontSave;
				LineMaterial.SetInt("_SrcBlend", 5);
				LineMaterial.SetInt("_DstBlend", 10);
				LineMaterial.SetInt("_Cull", 2);
				LineMaterial.SetInt("_ZWrite", 1);
			}
		}

		private void DestroyChildren(Transform tran)
		{
			for (int num = tran.childCount - 1; num >= 0; num--)
			{
				DestroyChildren(tran.GetChild(num).gameObject.transform);
				Object.Destroy(tran.GetChild(num).gameObject);
			}
		}

		public virtual void OnDestroy()
		{
			DestroyChildren(base.transform);
			Object.Destroy(BlueprintMeshFilter);
			Object.Destroy(LineMaterial);
			Object.Destroy(BlueprintRenderer);
			WireframeEdges.Clear();
		}

		public void OnRenderObject()
		{
			if (Camera.current == CameraController.Instance.StormCardCamera)
			{
				return;
			}
			CreateLineMaterial();
			LineMaterial.SetPass(0);
			_currentPosition = BlueprintTransform.position;
			_currentRotation = BlueprintTransform.rotation;
			_redraw = _currentPosition != _lastPosition || _currentRotation != _lastRotation;
			GL.Begin(1);
			GL.Color(BlueprintRenderer.material.color.SetAlpha(InventoryManager.Instance.CursorAlphaLine));
			foreach (Edge wireframeEdge in WireframeEdges)
			{
				if (_redraw)
				{
					wireframeEdge.CachedPoint1 = TransformPoint(wireframeEdge.Point1);
					wireframeEdge.CachedPoint2 = TransformPoint(wireframeEdge.Point2);
					_lastPosition = _currentPosition;
					_lastRotation = _currentRotation;
				}
				DrawLine(wireframeEdge.CachedPoint1, wireframeEdge.CachedPoint2);
			}
			GL.End();
		}

		private static void DrawLine(Vector3 v1, Vector3 v2)
		{
			GL.Vertex3(v1.x, v1.y, v1.z);
			GL.Vertex3(v2.x, v2.y, v2.z);
		}

		public static void DrawArrow(Vector3 pos, Vector3 direction, Color color, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20f)
		{
			DrawLine(pos, pos + direction);
			Vector3 vector = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 180f + arrowHeadAngle, 0f) * new Vector3(0f, 0f, 1f);
			Vector3 vector2 = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 180f - arrowHeadAngle, 0f) * new Vector3(0f, 0f, 1f);
			DrawLine(pos + direction, pos + direction + vector * arrowHeadLength);
			DrawLine(pos + direction, pos + direction + vector2 * arrowHeadLength);
		}
	}
}
