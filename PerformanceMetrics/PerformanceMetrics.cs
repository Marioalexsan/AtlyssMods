using BepInEx;
using HarmonyLib;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

namespace Marioalexsan.PerformanceMetrics;

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
public class PerformanceMetrics : BaseUnityPlugin
{
    private readonly Harmony _harmony = new Harmony($"{ModInfo.GUID}");
    private PerformanceGUI _performanceGUI = null!;

    private readonly List<(ProfilerRecorderHandle Handle, ProfilerRecorderDescription Description)> _availableMarkers = [];
    private readonly List<(Recorder Recorder, Sampler Sampler)> _availableSamplers = [];

    private readonly List<(ProfilerRecorder Recorder, ProfilerRecorderDescription Info)> _memoryStats = [];

    private ProfilerRecorder? _totalMemory;

    private bool _initialized;
    
    private void Awake()
    {
        Logger.LogInfo($"Currently running under a {(Debug.isDebugBuild ? "development" : "release" )} runner");
        
        if (!Debug.isDebugBuild)
            Logger.LogWarning("The release runner is not suitable for measurements since it has less metrics available overall!");
        
        _harmony.PatchAll();

        var rootObj = new GameObject(ModInfo.GUID + " performance monitor");
        GameObject.DontDestroyOnLoad(rootObj);
        _performanceGUI = rootObj.AddComponent<PerformanceGUI>();
    }

    private bool _lastActive;

    private void Update()
    {
        if (!_initialized)
        {
            _initialized = true;

            var markers = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(markers);

            var names = new List<string>();
            Sampler.GetNames(names);
        
            _availableSamplers.AddRange(names.Select(samplerName =>
            {
                Logger.LogInfo("Sampler: " + samplerName);
                var sampler = Sampler.Get(samplerName);
                return (sampler.GetRecorder(), sampler);
            }));

            for (int i = 0; i < markers.Count; i++)
            {
                _availableMarkers.Add((markers[i], ProfilerRecorderHandle.GetDescription(markers[i])));
            }

            var desiredCpuCounters = new HashSet<string>()
            {
                //"Total Used Memory",
                "CPU Render Thread Frame Time",
            };

            var desiredMemoryCounters = new HashSet<string>()
            {
                //"Total Used Memory",
                "Texture Memory",
                "Mesh Memory",
                "GC Used Memory",
                "Total Audio Memory",
                "Physics Used Memory",
                "Physics Used Memory (2D)",
                "Audio Used Memory",
            };
        
            for (int i = 0; i < _availableMarkers.Count; i++)
            {
                var info = _availableMarkers[i].Description;
                Logger.LogInfo($"Counter {info.Name} - {info.Category.Name}");
            
                if (desiredMemoryCounters.Contains(info.Name))
                {
                    _memoryStats.Add((ProfilerRecorder.Create(_availableMarkers[i].Handle, 1, ProfilerRecorderOptions.Default), info));
                }
            }

            var totalMemoryHandle = _availableMarkers.FirstOrDefault(x => x.Description.Name == "Total Used Memory").Handle;
            _totalMemory = totalMemoryHandle.Valid ? ProfilerRecorder.Create(totalMemoryHandle, 1, ProfilerRecorderOptions.Default) : null;
        }
        
        bool isActive = Input.GetKey(KeyCode.Alpha3);
        
        _performanceGUI.gameObject.SetActive(isActive);

        if (isActive && !_lastActive)
        {
            for (int i = 0; i < _memoryStats.Count; i++)
            {
                if (!_memoryStats[i].Recorder.IsRunning)
                    _memoryStats[i].Recorder.Start();
            }

            for (int i = 0; i < _availableSamplers.Count; i++)
                _availableSamplers[i].Recorder.enabled = true;
        }

        if (!isActive && _lastActive)
        {
            for (int i = 0; i < _memoryStats.Count; i++)
            {
                if (_memoryStats[i].Recorder.IsRunning)
                    _memoryStats[i].Recorder.Stop();
            }

            for (int i = 0; i < _availableSamplers.Count; i++)
                _availableSamplers[i].Recorder.enabled = false;
        }

        const float OneMiB = 1024 * 1024;
        
        if (isActive && _lastActive)
        {
            _performanceGUI.MemoryChart.Clear();

            for (int i = 0; i < _memoryStats.Count; i++)
            {
                _performanceGUI.MemoryChart.InsertAt(0, _memoryStats[i].Info.Name, _memoryStats[i].Recorder.CurrentValue / OneMiB);
            }
            const int MaxToKeep = 10;

            var maxTimes = new List<(string Name, long Value)>();
            long leftoverTimes = 0;

            for (int i = 0; i < _availableSamplers.Count; i++)
            {
                var recorder = _availableSamplers[i].Recorder;
                var samplerName = _availableSamplers[i].Sampler.name;

                maxTimes.Add((samplerName, recorder.elapsedNanoseconds));
                maxTimes.Sort((a, b) => b.Value.CompareTo(a.Value)); // Sort descending
                if (maxTimes.Count > MaxToKeep)
                {
                    leftoverTimes += maxTimes[^1].Value;
                    maxTimes.RemoveAt(maxTimes.Count - 1);
                }
            }
            
            _performanceGUI.PerformanceChart.Clear();

            for (int i = 0; i < maxTimes.Count; i++)
            {
                _performanceGUI.PerformanceChart.InsertAt(0, maxTimes[i].Name, maxTimes[i].Value / 1000000f); // Display as ms
            }
            
            _performanceGUI.PerformanceChart.InsertAt(maxTimes.Count, "Other", leftoverTimes / 1000000f); // Display as ms
        }

        _lastActive = isActive;
    }
}