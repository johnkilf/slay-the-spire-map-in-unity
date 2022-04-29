using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Map
{
    public class ScrollNonUI : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float tweenBackDuration = 0.3f;
        public Ease tweenBackEase;
        public bool freezeX;
        public FloatMinMax xConstraints = new FloatMinMax();
        public bool freezeY;
        public FloatMinMax yConstraints = new FloatMinMax();
        private Vector2 offset;
        // distance from the center of this Game Object to the point where we clicked to start dragging 
        private Vector3 pointerDisplacement;
        private float zDisplacement;
        private bool dragging;
        private Camera mainCamera;

        private GameObject scrolledObject;

        private void Awake()
        {
            mainCamera = Camera.main;
            zDisplacement = -mainCamera.transform.position.z + transform.position.z;
        }

        public void SetScrolledObject(GameObject scrolledObject)
        {
            this.scrolledObject = scrolledObject;
        }

        // returns mouse position in World coordinates for our GameObject to follow. 
        private Vector3 MouseInWorldCoords(Vector3 screenMousePos)
        {
            screenMousePos.z = zDisplacement;
            return mainCamera.ScreenToWorldPoint(screenMousePos);
        }

        private void TweenBack()
        {
            if (freezeY)
            {
                if (scrolledObject.transform.localPosition.x >= xConstraints.min && scrolledObject.transform.localPosition.x <= xConstraints.max)
                    return;

                var targetX = scrolledObject.transform.localPosition.x < xConstraints.min ? xConstraints.min : xConstraints.max;
                transform.DOLocalMoveX(targetX, tweenBackDuration).SetEase(tweenBackEase);
            }
            else if (freezeX)
            {
                Debug.Log("local pos is " + scrolledObject.transform.localPosition.y);
                Debug.Log("min is " + yConstraints.min);
                Debug.Log("max is " + yConstraints.max);
                if (scrolledObject.transform.localPosition.y >= yConstraints.min && scrolledObject.transform.localPosition.y <= yConstraints.max)
                    return;

                Debug.Log("Should be tweening back");
                var targetY = scrolledObject.transform.localPosition.y < yConstraints.min ? yConstraints.min : yConstraints.max;
                scrolledObject.transform.DOLocalMoveY(targetY, tweenBackDuration).SetEase(tweenBackEase);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            Debug.Log("Drag");
            var position = MouseInWorldCoords(eventData.position);
            scrolledObject.transform.position = new Vector3(
            freezeX ? scrolledObject.transform.position.x : position.x - pointerDisplacement.x,
            freezeY ? scrolledObject.transform.position.y : position.y - pointerDisplacement.y,
            scrolledObject.transform.position.z);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.Log("Pointer down");
            pointerDisplacement = -scrolledObject.transform.position + MouseInWorldCoords(eventData.position);
            Debug.Log("Pointer displacement is " + pointerDisplacement);
            transform.DOKill();
            dragging = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Debug.Log("Pointer up");
            dragging = false;
            TweenBack();
        }
    }
}
