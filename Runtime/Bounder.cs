using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Refactor.Ugui.Bounder
{
    /// <summary>Fits its RectTransform around contributing child RectTransforms after uGUI layout.</summary>
    /// <remarks>
    /// Active instances rebuild deepest-first during the canvas PostLayout pass. Width and height are
    /// independently optional; resizing preserves each direct child's world position and rect size.
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class Bounder : MonoBehaviour
    {
        /// <summary>Selects which geometric fact each direct child contributes.</summary>
        public enum ChildBoundsPolicy
        {
            /// <summary>Measures every contributing child's four world-space rect corners.</summary>
            Rect,

            /// <summary>Measures only each contributing child's pivot position.</summary>
            Pivot
        }

        [SerializeField] private bool includeInactive;
        [SerializeField] private bool fitWidth = true;
        [SerializeField] private bool fitHeight = true;
        [SerializeField] private RectOffset padding = new();
        [SerializeField] private ChildBoundsPolicy childBoundsPolicy = ChildBoundsPolicy.Rect;

        private RectTransform _target;
        private readonly Vector3[] _corners = new Vector3[4];

        private (RectTransform Transform, Vector3 Position, Vector2 Size)[] _children =
            Array.Empty<(RectTransform, Vector3, Vector2)>();

        #region Lifecycle

        private void OnEnable()
        {
            Initialize();
            Register(this);
        }

        private void OnDisable() => Unregister(this);

        private void OnTransformChildrenChanged() => Initialize();

        private void OnTransformParentChanged()
        {
            Unregister(this);
            Register(this);
        }

        /// <summary>Synchronously rebuilds all active Bounders in deepest-first hierarchy order.</summary>
        public static void RebuildAllNow() => ScanAll();

        private void Initialize()
        {
            _target = (RectTransform)transform;
            EnsureChildCapacity();
        }

        private void EnsureChildCapacity()
        {
            var required = _target.childCount;
            if (_children.Length < required)
                Array.Resize(ref _children, Mathf.NextPowerOfTwo(required));
        }

        #endregion

        #region Core

        private void Rebuild()
        {
            if (!fitWidth && !fitHeight)
                return;

            var scale = new Vector2(Mathf.Abs(_target.localScale.x), Mathf.Abs(_target.localScale.y));
            if (scale.x == 0f || scale.y == 0f)
                return;

            var areChildBoundsUnavailable = !TryMeasureChildren(out var bounds);
            if (areChildBoundsUnavailable)
            {
                FitEmpty();
                return;
            }

            var min = bounds.min - new Vector2(padding.left / scale.x, padding.bottom / scale.y);
            var max = bounds.max + new Vector2(padding.right / scale.x, padding.top / scale.y);
            var localPivot = new Vector2(
                fitWidth ? Mathf.Lerp(min.x, max.x, _target.pivot.x) : 0f,
                fitHeight ? Mathf.Lerp(min.y, max.y, _target.pivot.y) : 0f);
            var currentSize = _target.rect.size;
            var layoutSize = new Vector2(
                fitWidth ? max.x - min.x : currentSize.x,
                fitHeight ? max.y - min.y : currentSize.y);
            var pivotPosition = _target.TransformPoint(localPivot);
            var doesTargetSizeMatchLayout = (!fitWidth || currentSize.x == layoutSize.x) &&
                                            (!fitHeight || currentSize.y == layoutSize.y);
            if (doesTargetSizeMatchLayout && _target.position == pivotPosition)
                return;

            // Resizing the parent resolves Stretch children again; restore their visual layout afterward.
            var childCount = CaptureChildren();
            if (fitWidth)
                _target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, layoutSize.x);
            if (fitHeight)
                _target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, layoutSize.y);
            _target.position = pivotPosition;
            RestoreChildren(childCount);
        }

        private bool TryMeasureChildren(out Rect bounds)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var hasContributingChild = false;

            for (var childIndex = 0; childIndex < _target.childCount; childIndex++)
            {
                if (_target.GetChild(childIndex) is not RectTransform child ||
                    !includeInactive && !child.gameObject.activeInHierarchy)
                    continue;

                hasContributingChild = true;
                if (childBoundsPolicy == ChildBoundsPolicy.Pivot)
                {
                    Encapsulate(child.position, ref min, ref max);
                    continue;
                }

                child.GetWorldCorners(_corners);
                foreach (var corner in _corners)
                    Encapsulate(corner, ref min, ref max);
            }

            bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return hasContributingChild;
        }

        private void Encapsulate(Vector3 worldPoint, ref Vector2 min, ref Vector2 max)
        {
            Vector2 point = _target.InverseTransformPoint(worldPoint);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        private void FitEmpty()
        {
            var size = _target.rect.size;
            var areFittedAxesEmpty = (!fitWidth || size.x == 0f) && (!fitHeight || size.y == 0f);
            if (areFittedAxesEmpty)
                return;

            var childCount = CaptureChildren();
            if (fitWidth)
                _target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
            if (fitHeight)
                _target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
            RestoreChildren(childCount);
        }

        private int CaptureChildren()
        {
            var count = 0;
            for (var i = 0; i < _target.childCount; i++)
            {
                if (_target.GetChild(i) is not RectTransform child)
                    continue;

                _children[count++] = (child, child.position, child.rect.size);
            }

            return count;
        }

        private void RestoreChildren(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var child = _children[i];
                child.Transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, child.Size.x);
                child.Transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, child.Size.y);
                child.Transform.position = child.Position;
                _children[i] = default;
            }
        }

        #endregion

        // One shared post-layout pass scans every active Bounder deepest-first.
        // This is the only complete way to observe ordinary child RectTransform changes automatically.
        #region Canvas scan

        private static readonly List<Bounder> ActiveBounders = new();
        private static readonly Canvas.WillRenderCanvases QueueScanCallback = QueueScan;
        private static bool _depthOrderChanged = true;

        private static readonly ICanvasElement CanvasScan = new CanvasScanPass();

        private static readonly Comparison<Bounder> DeepestFirst =
            (left, right) => right.Depth.CompareTo(left.Depth);

        private int Depth
        {
            get
            {
                var depth = 0;
                for (var current = transform; current != null; current = current.parent)
                    depth++;
                return depth;
            }
        }

        private static void Register(Bounder bounder)
        {
            if (!bounder.isActiveAndEnabled ||
                !bounder.gameObject.scene.IsValid() ||
                ActiveBounders.Contains(bounder))
                return;

            if (ActiveBounders.Count == 0)
                Canvas.preWillRenderCanvases += QueueScanCallback;
            ActiveBounders.Add(bounder);
            _depthOrderChanged = true;
        }

        private static void Unregister(Bounder bounder)
        {
            if (!ActiveBounders.Remove(bounder))
                return;

            _depthOrderChanged = true;
            if (ActiveBounders.Count == 0)
                Canvas.preWillRenderCanvases -= QueueScanCallback;
        }

        private static void QueueScan()
        {
            if (ActiveBounders.Count > 0)
                CanvasUpdateRegistry.TryRegisterCanvasElementForLayoutRebuild(CanvasScan);
        }

        private static void ScanAll()
        {
            if (_depthOrderChanged)
            {
                // A parent must measure the result already fitted by its direct child.
                ActiveBounders.Sort(DeepestFirst);
                _depthOrderChanged = false;
            }

            foreach (var bounder in ActiveBounders) bounder.Rebuild();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Canvas.preWillRenderCanvases -= QueueScanCallback;
            PruneStaleRegistrations();
            _depthOrderChanged = true;
            if (ActiveBounders.Count > 0)
                Canvas.preWillRenderCanvases += QueueScanCallback;
        }

        private static void PruneStaleRegistrations()
        {
            for (var i = ActiveBounders.Count - 1; i >= 0; i--)
                if (ActiveBounders[i] == null || !ActiveBounders[i].isActiveAndEnabled)
                    ActiveBounders.RemoveAt(i);

            if (ActiveBounders.Count == 0)
                Canvas.preWillRenderCanvases -= QueueScanCallback;
        }

        private sealed class CanvasScanPass : ICanvasElement
        {
            Transform ICanvasElement.transform => ActiveBounders.Count == 0 ? null : ActiveBounders[0].transform;

            void ICanvasElement.Rebuild(CanvasUpdate phase)
            {
                if (phase == CanvasUpdate.PostLayout)
                    ScanAll();
            }

            void ICanvasElement.LayoutComplete()
            {
            }

            void ICanvasElement.GraphicUpdateComplete()
            {
            }

            bool ICanvasElement.IsDestroyed() => false;
        }

        #endregion
    }
}
