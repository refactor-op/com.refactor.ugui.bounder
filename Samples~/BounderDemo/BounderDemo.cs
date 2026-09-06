using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Refactor.Ugui.Bounder.Demo
{
    [DisallowMultipleComponent]
    public sealed class BounderDemo : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform[] examples;
        [SerializeField] private float rotationSpeed = 90f;

        private RectTransform _dragged;
        private Vector2 _dragOffset;
        private RectTransform _selected;
        private string _shortcuts;

        private void Start()
        {
            _shortcuts = $"Refactor.Ugui.Bounder 0.1.0\n1-{examples.Length} SWITCH · Q/E ROTATE · R RESET";
            SelectExample(0);
        }

        private void Update()
        {
            for (var index = 0; index < examples.Length && index < 9; index++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + index)))
                    SelectExample(index);

            var direction = 0f;
            if (Input.GetKey(KeyCode.Q)) direction++;
            if (Input.GetKey(KeyCode.E)) direction--;
            if (direction != 0f)
                _selected.Rotate(0f, 0f, direction * rotationSpeed * Time.unscaledDeltaTime);
            if (Input.GetKeyDown(KeyCode.R))
                _selected.localRotation = Quaternion.identity;
        }

        private void OnGUI() => GUILayout.Box(_shortcuts);

        /// <inheritdoc/>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (TryGetTarget(eventData, out var target))
                SelectTarget(target);
        }

        /// <inheritdoc/>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!TryGetTarget(eventData, out _dragged))
                return;

            SelectTarget(_dragged);
            var pointer = GetPointerInParent(_dragged, eventData);
            _dragOffset = (Vector2)_dragged.localPosition - pointer;
        }

        /// <inheritdoc/>
        public void OnDrag(PointerEventData eventData)
        {
            if (_dragged == null)
                return;

            var pointer = GetPointerInParent(_dragged, eventData);
            var position = pointer + _dragOffset;
            _dragged.localPosition = new Vector3(position.x, position.y, _dragged.localPosition.z);
        }

        /// <inheritdoc/>
        public void OnEndDrag(PointerEventData eventData) => _dragged = null;

        private static Vector2 GetPointerInParent(RectTransform target, PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)target.parent,
                eventData.position,
                eventData.pressEventCamera,
                out var pointer);
            return pointer;
        }

        private static bool TryGetTarget(PointerEventData eventData, out RectTransform target)
        {
            var hit = eventData.pointerPressRaycast.gameObject;
            if (hit != null && hit.TryGetComponent<Image>(out _))
            {
                target = (RectTransform)hit.transform;
                return true;
            }

            target = null;
            return false;
        }

        private void SelectExample(int index)
        {
            for (var i = 0; i < examples.Length; i++)
                examples[i].gameObject.SetActive(i == index);

            SelectTarget(examples[index]);
        }

        private void SelectTarget(RectTransform target)
        {
            _selected = target;
        }
    }
}
