﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Map
{
    public class MapView : MonoBehaviour
    {
        public enum MapOrientation
        {
            BottomToTop,
            TopToBottom,
            RightToLeft,
            LeftToRight
        }

        public MapOrientation orientation;

        
        public GameObject nodePrefab;

        [Tooltip("Offset of the top/bottom of the map from the edges of the screen")]
        public float orientationOffset;

        [Header("Background Settings")]
        [Tooltip("If the background sprite is null, background will not be shown")]
        public Sprite background;
        public Color32 backgroundColor = Color.white;
        [Tooltip("How much space to show between the nodes and the sides of the map")]
        public float backgroundXOffset;
        [Tooltip("How much space to show between the nodes and the top/bottom of the map")]
        public float backgroundYOffset;

        [Header("Line Settings")]
        public GameObject linePrefab;
        [Tooltip("Line point count should be > 2 to get smooth color gradients")]
        [Range(3, 10)]
        public int linePointsCount = 10;
        [Tooltip("Distance from the node till the line starting point")]
        public float offsetFromNodes = 0.5f;

        [Header("Colors")]
        [Tooltip("Node Visited or Attainable color")]
        public Color32 visitedColor = Color.white;
        [Tooltip("Locked node color")]
        public Color32 lockedColor = Color.gray;
        [Tooltip("Visited or available path color")]
        public Color32 lineVisitedColor = Color.white;
        [Tooltip("Unavailable path color")]
        public Color32 lineLockedColor = Color.gray;

        private GameObject outerMapParent;
        private GameObject scrollCollider;
        private GameObject mapParent;
        private Camera cam;
        // ALL nodes:
        public readonly List<MapNode> MapNodes = new List<MapNode>();
        private readonly List<LineConnection> lineConnections = new List<LineConnection>();

        private void Awake()
        {
            cam = Camera.main;
        }

        private void ClearMap()
        {
            if (outerMapParent != null)
                Destroy(outerMapParent);

            MapNodes.Clear();
            lineConnections.Clear();
        }

        public void ShowMap(Map m, MapConfig config)
        {
            if (m == null)
            {
                Debug.LogWarning("Map was null in MapView.ShowMap()");
                return;
            }

            ClearMap();

            CreateMapParent();

            CreateNodes(m.nodes, config.nodeBlueprints);

            DrawLines();

            SetCameraSize(m);

            SetOrientation(m);

            ResetNodesRotation();

            SetAttainableNodes(m);

            SetLineColors(m);

            CreateMapBackground(m);

            
        }

        private void SetCameraSize(Map m)
        {
            // TODO This assumes map is a vertical map - need to fix for horizontal maps too
            var mapSize = (m.MaximumXOffset() + backgroundXOffset) * 2;

            var minimumWidth = mapSize + (2 * backgroundXOffset);

            float screenAspect = (float)Screen.width / (float)Screen.height;

            float heightAtMinWidth = minimumWidth / screenAspect;

            cam.orthographicSize = heightAtMinWidth / 2;
        }

        private void CreateMapBackground(Map m)
        {
            if (background == null) return;

            var backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(mapParent.transform);
            var bossNode = MapNodes.FirstOrDefault(node => node.Node.nodeType == NodeType.Boss);
            var ySpan = m.DistanceBetweenFirstAndLastLayers();
            var xSpan = (m.MaximumXOffset() + backgroundXOffset ) * 2;
            backgroundObject.transform.localPosition = new Vector3(bossNode.transform.localPosition.x, ySpan / 2f, 0f);
            backgroundObject.transform.localRotation = Quaternion.identity;
            var sr = backgroundObject.AddComponent<SpriteRenderer>();
            sr.color = backgroundColor;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sprite = background;
            sr.size = new Vector2(xSpan, ySpan + backgroundYOffset * 2f);
        }

        private void CreateMapParent()
        {
            outerMapParent = new GameObject("OuterMapParent");
            scrollCollider = new GameObject("ScrollCollider");
            var scrollNonUi = scrollCollider.AddComponent<ScrollNonUI>();
            scrollNonUi.freezeX = orientation == MapOrientation.BottomToTop || orientation == MapOrientation.TopToBottom;
            scrollNonUi.freezeY = orientation == MapOrientation.LeftToRight || orientation == MapOrientation.RightToLeft;
            var boxCollider = scrollCollider.AddComponent<BoxCollider2D>();
            boxCollider.size = new Vector2(100, 100);
            scrollNonUi.transform.SetParent(outerMapParent.transform);


            // Move the map parent in front of the scroll box collider so that the nodes are clickable
            mapParent = new GameObject("MapParentWithAScroll");
            mapParent.transform.SetParent(outerMapParent.transform);
            mapParent.transform.position = mapParent.transform.position + new Vector3(0, 0, -1);

            scrollNonUi.SetScrolledObject(mapParent);

        }

        private void CreateNodes(IEnumerable<Node> nodes, List<NodeBlueprint> nodeBlueprints)
        {
            foreach (var node in nodes)
            {
                var mapNode = CreateMapNode(node, nodeBlueprints);
                MapNodes.Add(mapNode);
            }
        }

        private MapNode CreateMapNode(Node node, List<NodeBlueprint> nodeBlueprints)
        {
            var mapNodeObject = Instantiate(nodePrefab, mapParent.transform);
            var mapNode = mapNodeObject.GetComponent<MapNode>();
            var blueprint = GetBlueprint(node.blueprintName, nodeBlueprints);
            mapNode.SetUp(node, blueprint, lockedColor, visitedColor);
            mapNode.transform.localPosition = node.position;
            return mapNode;
        }

        public void SetAttainableNodes(Map currentMap)
        {
            // first set all the nodes as unattainable/locked:
            foreach (var node in MapNodes)
                node.SetState(NodeStates.Locked);

            if (currentMap.path.Count == 0)
            {
                // we have not started traveling on this map yet, set entire first layer as attainable:
                foreach (var node in MapNodes.Where(n => n.Node.point.y == 0))
                    node.SetState(NodeStates.Attainable);
            }
            else
            {
                // we have already started moving on this map, first highlight the path as visited:
                foreach (var point in currentMap.path)
                {
                    var mapNode = GetNode(point);
                    if (mapNode != null)
                        mapNode.SetState(NodeStates.Visited);
                }

                var currentPoint = currentMap.path[currentMap.path.Count - 1];
                var currentNode = currentMap.GetNode(currentPoint);

                // set all the nodes that we can travel to as attainable:
                foreach (var point in currentNode.outgoing)
                {
                    var mapNode = GetNode(point);
                    if (mapNode != null)
                        mapNode.SetState(NodeStates.Attainable);
                }
            }
        }

        public void SetLineColors(Map currentMap)
        {
            // set all lines to grayed out first:
            foreach (var connection in lineConnections)
                connection.SetColor(lineLockedColor);

            // set all lines that are a part of the path to visited color:
            // if we have not started moving on the map yet, leave everything as is:
            if (currentMap.path.Count == 0)
                return;

            // in any case, we mark outgoing connections from the final node with visible/attainable color:
            var currentPoint = currentMap.path[currentMap.path.Count - 1];
            var currentNode = currentMap.GetNode(currentPoint);

            foreach (var point in currentNode.outgoing)
            {
                var lineConnection = lineConnections.FirstOrDefault(conn => conn.from.Node == currentNode &&
                                                                            conn.to.Node.point.Equals(point));
                lineConnection?.SetColor(lineVisitedColor);
            }

            if (currentMap.path.Count <= 1) return;

            for (var i = 0; i < currentMap.path.Count - 1; i++)
            {
                var current = currentMap.path[i];
                var next = currentMap.path[i + 1];
                var lineConnection = lineConnections.FirstOrDefault(conn => conn.@from.Node.point.Equals(current) &&
                                                                            conn.to.Node.point.Equals(next));
                lineConnection?.SetColor(lineVisitedColor);
            }
        }

        private void SetOrientation(Map map)
        {
            var scrollNonUi = scrollCollider.GetComponent<ScrollNonUI>();
            var span = map.DistanceBetweenFirstAndLastLayers();
            var bossNode = MapNodes.FirstOrDefault(node => node.Node.nodeType == NodeType.Boss);
            Debug.Log("Map span in set orientation: " + span + " camera aspect: " + cam.aspect);

            // setting outerMapParent to be right in front of the camera first:
            outerMapParent.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);

            // TODO This probably only works for bottom to top maps
            float offset = -cam.orthographicSize + backgroundYOffset + orientationOffset;
            switch (orientation)
            {
                case MapOrientation.BottomToTop:
                    if (scrollNonUi != null)
                    {
                        scrollNonUi.yConstraints.max = 0;
                        scrollNonUi.yConstraints.min = -(span + 2f * offset);
                    }
                    outerMapParent.transform.localPosition += new Vector3(0, offset, 0);
                    break;
                case MapOrientation.TopToBottom:
                    mapParent.transform.eulerAngles = new Vector3(0, 0, 180);
                    if (scrollNonUi != null)
                    {
                        scrollNonUi.yConstraints.min = 0;
                        scrollNonUi.yConstraints.max = span + 2f * offset;
                    }
                    // factor in map span:
                    outerMapParent.transform.localPosition += new Vector3(0, -offset, 0);
                    break;
                case MapOrientation.RightToLeft:
                    offset *= cam.aspect;
                    mapParent.transform.eulerAngles = new Vector3(0, 0, 90);
                    // factor in map span:
                    outerMapParent.transform.localPosition -= new Vector3(offset, bossNode.transform.position.y, 0);
                    if (scrollNonUi != null)
                    {
                        scrollNonUi.xConstraints.max = span + 2f * offset;
                        scrollNonUi.xConstraints.min = 0;
                    }
                    break;
                case MapOrientation.LeftToRight:
                    offset *= cam.aspect;
                    mapParent.transform.eulerAngles = new Vector3(0, 0, -90);
                    outerMapParent.transform.localPosition += new Vector3(offset, -bossNode.transform.position.y, 0);
                    if (scrollNonUi != null)
                    {
                        scrollNonUi.xConstraints.max = 0;
                        scrollNonUi.xConstraints.min = -(span + 2f * offset);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void DrawLines()
        {
            foreach (var node in MapNodes)
            {
                foreach (var connection in node.Node.outgoing)
                    AddLineConnection(node, GetNode(connection));
            }
        }

        private void ResetNodesRotation()
        {
            foreach (var node in MapNodes)
                node.transform.rotation = Quaternion.identity;
        }

        public void AddLineConnection(MapNode from, MapNode to)
        {
            var lineObject = Instantiate(linePrefab, mapParent.transform);
            var lineRenderer = lineObject.GetComponent<LineRenderer>();
            var fromPoint = from.transform.position +
                            (to.transform.position - from.transform.position).normalized * offsetFromNodes;

            var toPoint = to.transform.position +
                          (from.transform.position - to.transform.position).normalized * offsetFromNodes;

            // drawing lines in local space:
            lineObject.transform.position = fromPoint;
            lineRenderer.useWorldSpace = false;

            // line renderer with 2 points only does not handle transparency properly:
            lineRenderer.positionCount = linePointsCount;
            for (var i = 0; i < linePointsCount; i++)
            {
                lineRenderer.SetPosition(i,
                    Vector3.Lerp(Vector3.zero, toPoint - fromPoint, (float)i / (linePointsCount - 1)));
            }

            var dottedLine = lineObject.GetComponent<DottedLineRenderer>();
            if (dottedLine != null) dottedLine.ScaleMaterial();

            lineConnections.Add(new LineConnection(lineRenderer, from, to));
        }

        private MapNode GetNode(Point p)
        {
            return MapNodes.FirstOrDefault(n => n.Node.point.Equals(p));
        }

        public NodeBlueprint GetBlueprint(string blueprintName, List<NodeBlueprint> nodeBlueprints)
        {
            return nodeBlueprints.FirstOrDefault(n => n.name == blueprintName);
        }
    }
}
