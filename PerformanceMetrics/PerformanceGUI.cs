using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UI;

namespace Marioalexsan.PerformanceMetrics;

public class PerformanceGUI : MonoBehaviour
{
    public PieChartWithLegend PerformanceChart { get; private set; } = null!;
    public PieChartWithLegend MemoryChart { get; private set; } = null!;
    public PieChartWithLegend GPUChart { get; private set; } = null!;
    
    private void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        var rectTransform = gameObject.AddComponent<RectTransform>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referencePixelsPerUnit = 100;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.physicalUnit = CanvasScaler.Unit.Points;
        scaler.fallbackScreenDPI = 96;
        scaler.defaultSpriteDPI = 96;
        scaler.dynamicPixelsPerUnit = 1;

        var performanceChart = new GameObject("PerformanceChart");
        var performanceChartRt = performanceChart.AddComponent<RectTransform>();
        performanceChart.transform.SetParent(gameObject.transform);
        
        performanceChartRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);
        performanceChartRt.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 20, 400);
        
        var performanceChartPie = performanceChart.AddComponent<PieChartWithLegend>();
        performanceChartPie.Size = new Vector2(250, 250);
        performanceChartPie.Title = "CPU Time (ms)";

        var memoryChart = new GameObject("MemoryChart");
        var memoryChartRt = memoryChart.AddComponent<RectTransform>();
        memoryChart.transform.SetParent(gameObject.transform);
        
        memoryChartRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);
        memoryChartRt.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 20, 400);
        
        var memoryChartPie = memoryChart.AddComponent<PieChartWithLegend>();
        memoryChartPie.Size = new Vector2(250, 250);
        memoryChartPie.Title = "Memory (MB)";

        var gpuChart = new GameObject("GPUChart");
        var gpuChartRt = gpuChart.AddComponent<RectTransform>();
        gpuChart.transform.SetParent(gameObject.transform);
        
        gpuChartRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300);
        gpuChartRt.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 20, 400);
        
        var gpuChartPie = gpuChart.AddComponent<PieChartWithLegend>();
        gpuChartPie.Size = new Vector2(250, 250);
        gpuChartPie.InsertAt(0, "Hello", 2);
        gpuChartPie.InsertAt(1, "World", 3);
        gpuChartPie.InsertAt(2, "Test", 4);
        gpuChartPie.Title = "GPU";
        
        performanceChart.transform.position = new Vector3(1720, 820);
        memoryChart.transform.position = new Vector3(1320, 820);
        gpuChart.transform.position = new Vector3(920, 820);

        PerformanceChart = performanceChartPie;
        MemoryChart = memoryChartPie;
        GPUChart = gpuChartPie;
    }
}