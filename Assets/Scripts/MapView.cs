﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapView : MonoBehaviour
{
    public enum MapOrientation
    {
        BottomToTop,
        TopToBottom,
        RightToLeft,
        LeftToRight
    }

    public MapManager mapManager;
    public MapOrientation orientation;
    public List<NodeBlueprint> blueprints;
    public GameObject nodePrefab;
    public float orientationOffset;
    [Header("Line settings")]
    public GameObject linePrefab;
    public int linePointsCount = 10;
    public float offsetFromNodes = 0.5f;
    [Header("Colors")]
    public Color32 visitedColor = Color.white;
    public Color32 lockedColor = Color.gray;
    public Color32 lineVisitedColor = Color.white;
    public Color32 lineLockedColor = Color.gray;
    
    private GameObject firstParent;
    private GameObject mapParent;
    private List<List<Point>> paths;
    // ALL nodes:
    public readonly List<MapNode> MapNodes = new List<MapNode>();
    private readonly List<LineConnection> lineConnections = new List<LineConnection>();
    
    public static MapView Instance;

    private void Awake()
    {
        Instance = this;
    }

    private void ClearMap()
    {
        if (firstParent != null)
            Destroy(firstParent);
        
        MapNodes.Clear();
        lineConnections.Clear();
    }

    public void ShowMap(Map m)
    {
        if (m == null)
        {
            Debug.LogWarning("Map was null in MapView.ShowMap()");
            return;
        }

        ClearMap();
        
        CreateMapParent();

        CreateNodes(m.nodes);

        DrawLines();
        
        SetOrientation();
        
        ResetNodesRotation();

        SetAttainableNodes();

        SetLineColors();
    }

    private void CreateMapParent()
    {
        firstParent = new GameObject("OuterMapParent");
        mapParent = new GameObject("MapParentWithAScroll");
        mapParent.transform.SetParent(firstParent.transform);
        var scrollNonUi = mapParent.AddComponent<ScrollNonUI>();
        scrollNonUi.freezeX = orientation == MapOrientation.BottomToTop || orientation == MapOrientation.TopToBottom;
        scrollNonUi.freezeY = orientation == MapOrientation.LeftToRight || orientation == MapOrientation.RightToLeft;
        var boxCollider = mapParent.AddComponent<BoxCollider>();
        boxCollider.size = new Vector3(100, 100, 1);
    }

    private void CreateNodes(IEnumerable<Node> nodes)
    {
        foreach (var node in nodes)
        {
            var mapNode = CreateMapNode(node);
            MapNodes.Add(mapNode);
        }
    }

    private MapNode CreateMapNode(Node node)
    {
        var mapNodeObject = Instantiate(nodePrefab, mapParent.transform);
        var mapNode = mapNodeObject.GetComponent<MapNode>();
        mapNode.SetUp(node);
        mapNode.transform.localPosition = node.position;
        return mapNode;
    }

    public void SetAttainableNodes()
    {
        // first set all the nodes as unattainable/locked:
        foreach (var node in MapNodes)
            node.SetState(NodeStates.Locked);
        
        if (mapManager.CurrentMap.path.Count == 0)
        {
            // we have not started traveling on this map yet, set entire first layer as attainable:
            foreach (var node in MapNodes.Where(n => n.Node.point.y == 0))
                node.SetState(NodeStates.Attainable);
        }
        else
        {
            // we have already started moving on this map, first highlight the path as visited:
            foreach (var point in mapManager.CurrentMap.path)
            {
                var mapNode = GetNode(point);
                if (mapNode != null)
                    mapNode.SetState(NodeStates.Visited);
            }

            var currentPoint = mapManager.CurrentMap.path[mapManager.CurrentMap.path.Count - 1];
            var currentNode = mapManager.CurrentMap.GetNode(currentPoint);
            
            // set all the nodes that we can travel to as attainable:
            foreach (var point in currentNode.outgoing)
            {
                var mapNode = GetNode(point);
                if (mapNode != null)
                    mapNode.SetState(NodeStates.Attainable);
            }
        }
    }

    public void SetLineColors()
    {
        // set all lines to grayed out first:
        foreach (var connection in lineConnections)
            connection.SetColor(lineLockedColor);
        
        // set all lines that are a part of the path to visited color:
        // if we have not started moving on the map yet, leave everything as is:
        if (mapManager.CurrentMap.path.Count == 0)
            return;
        
        // in any case, we mark outgoing connections from the final node with visible/attainable color:
        var currentPoint = mapManager.CurrentMap.path[mapManager.CurrentMap.path.Count - 1];
        var currentNode = mapManager.CurrentMap.GetNode(currentPoint);
            
        foreach (var point in currentNode.outgoing)
        {
            var lineConnection = lineConnections.FirstOrDefault(conn => conn.from.Node == currentNode &&
                                                                        conn.to.Node.point.Equals(point));
            lineConnection?.SetColor(lineVisitedColor);
        }

        if (mapManager.CurrentMap.path.Count <= 1) return;
        
        for (var i = 0; i < mapManager.CurrentMap.path.Count - 1; i++)
        {
            var current = mapManager.CurrentMap.path[i];
            var next = mapManager.CurrentMap.path[i + 1];
            var lineConnection = lineConnections.FirstOrDefault(conn => conn.@from.Node.point.Equals(current) &&
                                                                        conn.to.Node.point.Equals(next));
            lineConnection?.SetColor(lineVisitedColor);
        }
    }

    private void SetOrientation()
    {
        var scrollNonUi = mapParent.GetComponent<ScrollNonUI>();
        var span = mapManager.CurrentMap.DistanceBetweenFirstAndLastLayers();
        var cameraDimension = orientation == MapOrientation.LeftToRight || orientation == MapOrientation.RightToLeft
            ? GetCameraWidth()
            : GetCameraHeight();
        var constraint = Mathf.Max(0f, span - cameraDimension);
        var bossNode = MapNodes.FirstOrDefault(node => node.Node.nodeType == NodeType.Boss);
        Debug.Log("Map span in set orientation: " + span + " camera dimension: " + cameraDimension);

        switch (orientation)
        {
            case MapOrientation.BottomToTop:
                if (scrollNonUi != null)
                {
                    scrollNonUi.yConstraints.max = 0;
                    scrollNonUi.yConstraints.min = -(constraint - orientationOffset);
                }
                firstParent.transform.localPosition += new Vector3(0, orientationOffset / 2, 0);
                break;
            case MapOrientation.TopToBottom:
                mapParent.transform.eulerAngles = new Vector3(0, 0, 180);
                if (scrollNonUi != null)
                {
                    scrollNonUi.yConstraints.min = 0;
                    scrollNonUi.yConstraints.max = constraint - orientationOffset;
                }
                // factor in map span:
                firstParent.transform.localPosition += new Vector3(0, -orientationOffset / 2, 0);
                break;
            case MapOrientation.RightToLeft:
                mapParent.transform.eulerAngles = new Vector3(0, 0, 90);
                // factor in map span:
                firstParent.transform.localPosition -= new Vector3(orientationOffset, bossNode.transform.position.y, 0);
                if (scrollNonUi != null)
                {
                    scrollNonUi.xConstraints.max = constraint - orientationOffset;
                    scrollNonUi.xConstraints.min = 0;
                }
                break;
            case MapOrientation.LeftToRight:
                mapParent.transform.eulerAngles = new Vector3(0, 0, -90);
                firstParent.transform.localPosition += new Vector3(orientationOffset, -bossNode.transform.position.y, 0);
                if (scrollNonUi != null)
                {
                    scrollNonUi.xConstraints.max = 0;
                    scrollNonUi.xConstraints.min = -(constraint - orientationOffset);
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
                Vector3.Lerp(Vector3.zero, toPoint - fromPoint, (float) i / (linePointsCount - 1)));
        }
        
        var dottedLine = lineObject.GetComponent<DottedLineRenderer>();
        if(dottedLine != null) dottedLine.ScaleMaterial();

        lineConnections.Add(new LineConnection(lineRenderer, from, to));
    }

    private MapNode GetNode(Point p)
    {
        return MapNodes.FirstOrDefault(n => n.Node.point.Equals(p));
    }

    public NodeBlueprint GetBlueprint(NodeType type)
    {
        return blueprints.FirstOrDefault(n => n.nodeType == type);
    }

    private static float GetCameraWidth()
    {
        var cam = Camera.main;
        if (cam == null) return 0;
        var height = 2f * cam.orthographicSize; 
        return height * cam.aspect;
    }
    
    private static float GetCameraHeight()
    {
        var cam = Camera.main;
        if (cam == null) return 0;
        return 2f * cam.orthographicSize;
    }
}
