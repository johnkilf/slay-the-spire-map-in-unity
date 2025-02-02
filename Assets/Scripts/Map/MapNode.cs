﻿using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Map
{
    public enum NodeStates
    {
        Locked,
        Visited,
        Attainable
    }
}

namespace Map
{
    public class MapNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public SpriteRenderer sr;
        public SpriteRenderer visitedCircle;
        public Image visitedCircleImage;


        public Node Node { get; private set; }
        public NodeBlueprint Blueprint { get; private set; }

        private float initialScale;
        private const float HoverScaleFactor = 1.2f;
        private float mouseDownTime;

        private const float MaxClickDuration = 0.5f;


        // Cached values from MapView
        private Color visitedColor;
        private Color lockedColor;


        public static event Action<MapNode> nodeClicked;

        public void SetUp(Node node, NodeBlueprint blueprint, Color lockedColor, Color visitedColor)
        {
            this.lockedColor = lockedColor;
            this.visitedColor = visitedColor;
            
            Node = node;
            Blueprint = blueprint;
            sr.sprite = blueprint.sprite;
            if (node.nodeType == NodeType.Boss) transform.localScale *= 1.5f;
            initialScale = sr.transform.localScale.x;
            visitedCircle.color = visitedColor;
            visitedCircle.gameObject.SetActive(false);
            SetState(NodeStates.Locked);
        }

        public void SetState(NodeStates state)
        {
            visitedCircle.gameObject.SetActive(false);
            switch (state)
            {
                case NodeStates.Locked:
                    sr.DOKill();
                    sr.color = lockedColor;
                    break;
                case NodeStates.Visited:
                    sr.DOKill();
                    sr.color = visitedColor;
                    visitedCircle.gameObject.SetActive(true);
                    break;
                case NodeStates.Attainable:
                    // start pulsating from visited to locked color:
                    sr.color = lockedColor;
                    sr.DOKill();
                    sr.DOColor(visitedColor, 0.5f).SetLoops(-1, LoopType.Yoyo);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        public void ShowSwirlAnimation()
        {
            if (visitedCircleImage == null)
                return;

            const float fillDuration = 0.3f;
            visitedCircleImage.fillAmount = 0;

            DOTween.To(() => visitedCircleImage.fillAmount, x => visitedCircleImage.fillAmount = x, 1f, fillDuration);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            sr.transform.DOKill();
            sr.transform.DOScale(initialScale * HoverScaleFactor, 0.3f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            sr.transform.DOKill();
            sr.transform.DOScale(initialScale, 0.3f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            mouseDownTime = Time.time;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (Time.time - mouseDownTime < MaxClickDuration)
            {
                nodeClicked.Invoke(this);
                // user clicked on this node:
                
            }
        }
    }
}
