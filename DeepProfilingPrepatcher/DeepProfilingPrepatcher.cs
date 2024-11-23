using BepInEx;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using MonoMod.Cil;
using MonoMod.Utils;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static class Patcher
{
    public static IEnumerable<string> TargetDLLs { get; } = [
        "UnityEngine.CoreModule.dll",
        "Assembly-CSharp.dll"
        ];

    public static TypeReference ProfilerMarkerType = null!;
    public static MethodReference ProfilerMarkerConstructor = null!;
    public static MethodReference ProfilerMarkerBegin = null!;
    public static MethodReference ProfilerMarkerEnd = null!;
    public static int MarkersSoFar = 0;

    public static TypeDefinition MarkerBuilderHolder = null!;

    public static readonly string[] BannedMethods = [
        ".cctor",
        ".ctor",
        ".ctor",
        "Awake",
        "Start",
        "FixedUpdate",
        "LateUpdate",
        "Invoke",
        "BeginInvoke",
        "EndInvoke",
        "Prelayout",
        "Layout",
        "PostLayout",
        "PreRender",
        "LatePreRender"
        ];

    public static readonly string[] MethodsToWrap = [
        "UnityEngine.Behaviour::get_enabled",
        "UnityEngine.Behaviour::set_enabled",
        "UnityEngine.Renderer::get_enabled",
        "UnityEngine.Renderer::set_enabled",
        "UnityEngine.Terrain::get_drawHeightmap",
        "UnityEngine.Terrain::set_drawHeightmap",
        "UnityEngine.Terrain::get_drawTreesAndFoliage",
        "UnityEngine.Terrain::set_drawTreesAndFoliage",
        ];

    public static AssemblyDefinition UnityCoreModule = null!;

    public static Dictionary<string, FieldDefinition> MarkerFields = [];
    public static HashSet<MethodReference> PatchedMethods = [];

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void SetupUnity(AssemblyDefinition assembly)
    {
        UnityCoreModule = assembly;
        var reference = assembly.MainModule.Types.First(x => x.Name == "ProfilerMarker");
        ProfilerMarkerType = reference;
        ProfilerMarkerConstructor = reference.GetConstructors().Where(x => x.Parameters.Count() == 1).First();
        ProfilerMarkerBegin = reference.GetMethods().Where(x => x.Name == "Begin" && x.Parameters.Count() == 0).First();
        ProfilerMarkerEnd = reference.GetMethods().Where(x => x.Name == "End" && x.Parameters.Count() == 0).First();
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void SetupGame(AssemblyDefinition assembly)
    {
        var reference = UnityCoreModule.MainModule.Types.First(x => x.Name == "ProfilerMarker");
        ProfilerMarkerType = assembly.MainModule.ImportReference(reference);
        ProfilerMarkerConstructor = assembly.MainModule.ImportReference(reference.GetConstructors().Where(x => x.Parameters.Count() == 1).First());
        ProfilerMarkerBegin = assembly.MainModule.ImportReference(reference.GetMethods().Where(x => x.Name == "Begin" && x.Parameters.Count() == 0).First());
        ProfilerMarkerEnd = assembly.MainModule.ImportReference(reference.GetMethods().Where(x => x.Name == "End" && x.Parameters.Count() == 0).First());
    }

    public static void Patch(AssemblyDefinition assembly)
    {
        MarkerBuilderHolder = null!;
        MarkerFields.Clear();
        PatchedMethods.Clear();

        Console.WriteLine($"Grabbing profiler marker methods.");

        if (assembly.MainModule.Name == "UnityEngine.CoreModule.dll")
            SetupUnity(assembly);

        if (assembly.MainModule.Name == "Assembly-CSharp.dll")
            SetupGame(assembly);

        var allMethodsToMark = assembly.MainModule.Name switch
        {
            "Assembly-CSharp.dll" => assembly.MainModule.Types
                .SelectMany(x => x.Methods)
                .Where(x => !x.IsAbstract && !x.IsInternalCall && !x.IsGenericInstance)
                .Where(x => !BannedMethods.Any(name => x.Name == name))
                .ToList(),
            "UnityEngine.CoreModule.dll" => [],
            //"UnityEngine.CoreModule.dll" => assembly.MainModule.Types
            //    .Where(x => x.Namespace.StartsWith("UnityEngine"))
            //    .SelectMany(x => x.Methods)
            //    .Where(x => UnityTypesToPatch.Any(name => x.DeclaringType.FullName == name))
            //    .Where(x => !x.IsAbstract && !x.IsInternalCall && !x.IsGenericInstance && x.IsPublic)
            //    .ToList(),
            _ => throw new InvalidOperationException("Invalid DLL")
        };

        Console.WriteLine($"Patching performance metric for {allMethodsToMark.Count} methods in {assembly.MainModule.Name}.");

        foreach (var method in allMethodsToMark)
        {
            try
            {
                MeasurePerformanceMethod(assembly, method);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Crash in MeasurePerformanceMethod {method.Name}");
                Console.WriteLine(e);
            }
        }

        int callSites = 0;

        Console.WriteLine("Checking " + Path.Combine(Path.GetDirectoryName(typeof(Patcher).Assembly.Location), "nowrap.txt"));
        if (!File.Exists(Path.Combine(Path.GetDirectoryName(typeof(Patcher).Assembly.Location), "nowrap.txt")))
        {
            foreach (var method in allMethodsToMark)
            {
                try
                {
                    MeasurePerformanceWrapCalls(assembly, method, ref callSites);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Crash in MeasurePerformanceWrapCalls {method.Name}");
                    Console.WriteLine(e);
                }
            }

            Console.WriteLine($"Patched performance metrics for {allMethodsToMark.Count} methods in {assembly.MainModule.Name}!");
            Console.WriteLine($"Total markers: {MarkerFields.Count}");
            Console.WriteLine($"Total callsites: {callSites}");
        }
    }

    public static FieldDefinition CreateStaticMarker(AssemblyDefinition assembly, MethodReference method)
    {
        if (MarkerFields.TryGetValue(method.FullName, out var marker))
            return marker;

        if (MarkerBuilderHolder == null)
        {
            MarkerBuilderHolder = new TypeDefinition("Marioalexsan.DeepProfilingPrepatcher", "ProfilerMarkerBuilderHolder", TypeAttributes.AnsiClass | TypeAttributes.Public, assembly.MainModule.TypeSystem.Object);
            assembly.MainModule.Types.Add(MarkerBuilderHolder);

            var constructor = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, assembly.MainModule.TypeSystem.Void);
            MarkerBuilderHolder.Methods.Add(constructor);

            constructor.Body.InitLocals = true;
            var processor = constructor.Body.GetILProcessor();

            processor.Emit(OpCodes.Nop);
            processor.Emit(OpCodes.Ret);
        }

        var markerField = new FieldDefinition("performanceMarkerPrepatcher_" + (MarkersSoFar++).ToString(), Mono.Cecil.FieldAttributes.Static | Mono.Cecil.FieldAttributes.Public, ProfilerMarkerType);
        MarkerBuilderHolder.Fields.Add(markerField);

        var staticConstructor = MarkerBuilderHolder.GetStaticConstructor();

        var worker = staticConstructor.Body.GetILProcessor();
        var top = staticConstructor.Body.Instructions[0];

        worker.InsertBefore(top, worker.Create(OpCodes.Ldstr, $"[{MarkerFields.Count}] {method.FullName}"));
        worker.InsertBefore(top, worker.Create(OpCodes.Newobj, ProfilerMarkerConstructor));
        worker.InsertBefore(top, worker.Create(OpCodes.Stsfld, markerField));

        MarkerFields[method.FullName] = markerField;
        return markerField;
    }

    public static void MeasurePerformanceWrapCalls(AssemblyDefinition assembly, MethodDefinition caller, ref int callSites)
    {
        FieldDefinition? markerField = null;

        var worker = caller.Body.GetILProcessor();

        foreach (var inst in caller.Body.Instructions.ToList())
        {
            if (inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt)
            {
                var opcode = inst.OpCode;
                var operand = (MethodReference)inst.Operand;

                if (PatchedMethods.Contains(operand) || !MethodsToWrap.Any(x => operand.FullName.Contains(x)))
                {
                    continue;
                }

                callSites++;

                markerField = CreateStaticMarker(assembly, operand);

                Instruction ins1, ins2, ins3, ins4, ins5;

                ins1 = inst;

                worker.InsertAfter(inst, ins5 = worker.Create(OpCodes.Nop));
                worker.InsertAfter(inst, ins4 = worker.Create(OpCodes.Nop));
                worker.InsertAfter(inst, ins3 = worker.Create(OpCodes.Nop));
                worker.InsertAfter(inst, ins2 = worker.Create(OpCodes.Nop));

                ins1.OpCode = OpCodes.Ldsflda;
                ins1.Operand = markerField;

                ins2.OpCode = OpCodes.Callvirt;
                ins2.Operand = ProfilerMarkerBegin;

                ins3.OpCode = opcode;
                ins3.Operand = operand;

                ins4.OpCode = OpCodes.Ldsflda;
                ins4.Operand = markerField;

                ins5.OpCode = OpCodes.Callvirt;
                ins5.Operand = ProfilerMarkerEnd;
            }
        }

        RecheckLongJumps(caller);
    }

    public static void MeasurePerformanceMethod(AssemblyDefinition assembly, MethodDefinition method)
    {
        FieldDefinition markerField = CreateStaticMarker(assembly, method);

        var worker = method.Body.GetILProcessor();
        var first = method.Body.Instructions.First();
        var last = method.Body.Instructions.Last();

        if (last.OpCode != OpCodes.Ret)
        {
            Console.WriteLine($"{method.Name} doesn't have OpCodes.Ret at the end!");
            return;
        }

        worker.InsertBefore(first, worker.Create(OpCodes.Ldsflda, markerField));
        worker.InsertBefore(first, worker.Create(OpCodes.Callvirt, ProfilerMarkerBegin));

        Instruction last1, last2;

        worker.InsertAfter(last, last2 = worker.Create(OpCodes.Nop));
        worker.InsertAfter(last, last1 = worker.Create(OpCodes.Nop));

        last.OpCode = OpCodes.Ldsflda;
        last.Operand = markerField;

        last1.OpCode = OpCodes.Callvirt;
        last1.Operand = ProfilerMarkerEnd;

        last2.OpCode = OpCodes.Ret;
        last2.Operand = null;

        RecheckLongJumps(method);

        foreach (var inst in method.Body.Instructions)
        {
            if (inst.OpCode == OpCodes.Ret && inst != last && inst != last1 && inst != last2)
            {
                inst.OpCode = OpCodes.Br;
                inst.Operand = last;
                //Console.WriteLine($"Ret redirect: {method.Name} prev ret {inst.Offset} to ret {last.Offset}");
            }
        }

        PatchedMethods.Add(method);
    }

    public static void RecheckLongJumps(MethodDefinition method)
    {
        method.RecalculateILOffsets();

        foreach (var inst in method.Body.Instructions)
        {
            static bool IsLongJump(Instruction inst, out int diff)
            {
                return (diff = Math.Abs(((Instruction)inst.Operand).Offset - inst.Offset)) >= sbyte.MaxValue;
            }

            var prev = inst.OpCode;
            var diff = 0;

            if (inst.OpCode == OpCodes.Brfalse_S && IsLongJump(inst, out diff))
                inst.OpCode = OpCodes.Brfalse;

            if (inst.OpCode == OpCodes.Brtrue_S && IsLongJump(inst, out diff))
                inst.OpCode = OpCodes.Brtrue;

            if (inst.OpCode == OpCodes.Br_S && IsLongJump(inst, out diff))
                inst.OpCode = OpCodes.Br;
        }
    }
}