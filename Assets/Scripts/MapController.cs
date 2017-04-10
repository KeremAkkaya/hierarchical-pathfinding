﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public class MapController : MonoBehaviour {

    public EventSystem eventSys;

    private List<FileInfo> maps;

    private SceneMapDisplay mapDisplay;
    private UIController uiCtrl;

    private Map map;
    private Graph graph;

	// Use this for initialization
	void Start () {
        mapDisplay = GetComponent<SceneMapDisplay>();
        uiCtrl = GetComponent<UIController>();

        //Populate list of maps
        maps = Map.GetMaps();
        uiCtrl.FillMaps(maps.Select((FileInfo f) => f.Name).ToList());
	}

    public void LoadMap()
    {
        FileInfo current = maps[uiCtrl.DdlMaps.value];
        int ClusterSize = uiCtrl.Cluster.GetValue();
        int LayerDepth = uiCtrl.Layers.GetValue();

        map = Map.LoadMap(current.FullName);

        float deltaT;
        graph = RunGenerateGraph(LayerDepth, ClusterSize, out deltaT);
        uiCtrl.ClusterTime.text = string.Format("{0} s", deltaT);

        mapDisplay.SetMap(map, graph);

        //TODO: Populate layer select dropdown
    }


    private Graph RunGenerateGraph(int LayerDepth, int ClusterSize, out float deltatime) {
        float before, after;

        before = Time.realtimeSinceStartup;
        graph = new Graph(map, LayerDepth, ClusterSize);
        after = Time.realtimeSinceStartup;

        deltatime = after - before;
        return graph;
    }

    private TestResult RunPathfind(GridTile start, GridTile dest)
    {
        float before, after;
        TestResult result = new TestResult();

        PathfindResult res = new PathfindResult();
        before = Time.realtimeSinceStartup;
        res.Path = HierarchicalPathfinder.FindPath(graph, start, dest);
        after = Time.realtimeSinceStartup;

        res.RunningTime = after - before;
        res.CalculatePathLength();
        result.HPAStarResult = res;

        res = new PathfindResult();
        before = Time.realtimeSinceStartup;
        res.Path = Pathfinder.FindPath(start, dest, map.Boundaries, map.Obstacles);
        after = Time.realtimeSinceStartup;

        res.RunningTime = after - before;
        res.CalculatePathLength();
        result.AStarResult = res;

        return result;
    }

    public void RunBenchmark()
    {
        FileInfo current = maps[uiCtrl.DdlMaps.value];

        TestResults results = new TestResults()
        {
            MapName = current.Name,
            ClusterSize = uiCtrl.Cluster.GetValue(),
            Layers = uiCtrl.Layers.GetValue()
        };

        map = Map.LoadMap(current.FullName);
        graph = RunGenerateGraph(results.Layers, results.ClusterSize, out results.GenerateClusterTime);

        List<TestCase> testcases = Benchmark.LoadTestCases(current.Name);
        
        TestResult res;
        foreach (TestCase testcase in testcases)
        {
            res = RunPathfind(testcase.Start, testcase.destination);
            res.GroupingNumber = testcase.GroupingNumber;
            results.results.Add(res);
        }

        //Write results in file
        Benchmark.WriteResults(results);
    }

    public void FindPath()
    {
        GridTile start = uiCtrl.Source.GetPositionField();
        GridTile dest = uiCtrl.Destination.GetPositionField();

        TestResult res = RunPathfind(start, dest);

        uiCtrl.HPAStarTime.text = string.Format("{0} s", res.HPAStarResult.RunningTime);
        
        uiCtrl.AStarTime.text = string.Format("{0} s", res.AStarResult.RunningTime);

        //Display the result
        mapDisplay.DrawPaths(res.HPAStarResult.Path, res.AStarResult.Path);
    }


    // Update is called once per frame
    void Update()
    {
        if (!EventSystem.current.IsPointerOverGameObject()) {
            mapDisplay.HandleZoom();
            mapDisplay.HandleCameraMove();
            mapDisplay.HandleCameraReset();

            SelectGridPos();
        }
    }

    void SelectGridPos()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction, 100f);

            if (hit && hit.collider.tag == "GridTile")
            {
                Vector3 localHitPoint = transform.worldToLocalMatrix.MultiplyPoint(hit.point);
                GridTile pos = new GridTile(localHitPoint);

                //Be sure that it's a valid position
                if (map.Obstacles[pos.y][pos.x])
                {
                    EditorUtility.DisplayDialog("Info", "You cannot select a tile marked as an obstacle.", "Ok");
                    return;
                }

                uiCtrl.SetPosition(pos);
            }
        }
    }
}
