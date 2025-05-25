using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using MonoMod.Cil;
using MonoMod.Utils;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Linq;
using Unity.Profiling;
using UnityEngine;

public static class Patcher
{
    public static IEnumerable<string> TargetDLLs { get; } = ["Assembly-CSharp.dll"];

    public static IEnumerable<string> MethodsToPatch { get; } = [
        "PlayerEntity_RenderEquipDisplay",
        "Remove_HelmDisplay",
        "Remove_ChestPieceDisplay",
        "Remove_LeggingsDisplay",
        "Remove_CapeDisplay",
        "Apply_ArmorDisplay",
        "Apply_HelmDisplay",
        "Apply_ChestPieceDisplay",
        "Apply_LeggingsDisplay",
        "Apply_CapeDisplay",
        "Apply_CharacterDisplay",
        "Apply_CharacterDisplay",
        "Handle_ShieldRenderingDisplay",
        "Apply_ShieldDisplay",
        "Remove_ShieldDisplay",
        "Handle_WeaponRenderingDisplay",
        "Apply_WeaponDisplay",
        "Remove_WeaponDisplay",
        "Init_WeaponDisplayMesh",
        ];

    public static TypeReference ProfilerMarkerType = null!;
    public static MethodReference ProfilerMarkerConstructor = null!;
    public static MethodReference ProfilerMarkerBegin = null!;
    public static MethodReference ProfilerMarkerEnd = null!;
    public static int MarkersSoFar = 0;

    public static void Patch(AssemblyDefinition assembly)
    {
        Console.WriteLine($"Grabbing profiler marker methods.");

        ProfilerMarkerType = assembly.MainModule.ImportReference(typeof(ProfilerMarker));
        ProfilerMarkerConstructor = assembly.MainModule.ImportReference(typeof(ProfilerMarker).GetConstructors().Where(x => x.GetParameters().Count() == 1).First());
        ProfilerMarkerBegin = assembly.MainModule.ImportReference(typeof(ProfilerMarker).GetMethods().Where(x => x.Name == "Begin" && x.GetParameters().Count() == 0).First());
        ProfilerMarkerEnd = assembly.MainModule.ImportReference(typeof(ProfilerMarker).GetMethods().Where(x => x.Name == "End" && x.GetParameters().Count() == 0).First());

        var allMethodsToMark = assembly.MainModule.Types.SelectMany(x => x.Methods).Where(x => MethodsToPatch.Any(y => x.Name.Contains(y))).ToList();

        Console.WriteLine($"Patching performance metric for {allMethodsToMark.Count} methods.");

        foreach (var method in allMethodsToMark)
        {
            Console.WriteLine($"Patching performance metric for method: {method.Name}");
            MeasurePerformanceMethod(method, CreateStaticMarker(method.DeclaringType, method.Name));
        }

        Console.WriteLine($"Done!");
    }

    public static FieldDefinition CreateStaticMarker(TypeDefinition type, string name)
    {
        var markerField = new FieldDefinition("performanceMarkerPrepatcher_" + (MarkersSoFar++).ToString(), Mono.Cecil.FieldAttributes.Static | Mono.Cecil.FieldAttributes.Private, ProfilerMarkerType);
        type.Fields.Add(markerField);

        var staticConstructor = type.GetStaticConstructor();

        var worker = staticConstructor.Body.GetILProcessor();
        var top = staticConstructor.Body.Instructions[0];

        worker.InsertBefore(top, worker.Create(OpCodes.Ldstr, name));
        worker.InsertBefore(top, worker.Create(OpCodes.Newobj, ProfilerMarkerConstructor));
        worker.InsertBefore(top, worker.Create(OpCodes.Stsfld, markerField));

        return markerField;
    }

    public static void MeasurePerformanceMethod(MethodDefinition method, FieldDefinition markerField)
    {
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

        Instruction cleanupInst;

        worker.InsertBefore(last, cleanupInst = worker.Create(OpCodes.Ldsflda, markerField));
        worker.InsertBefore(last, worker.Create(OpCodes.Callvirt, ProfilerMarkerEnd));

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

            if (inst.OpCode == OpCodes.Brfalse_S || inst.OpCode == OpCodes.Brfalse || inst.OpCode == OpCodes.Brtrue_S || inst.OpCode == OpCodes.Brtrue || inst.OpCode == OpCodes.Br_S || inst.OpCode == OpCodes.Br)
            {
                Console.WriteLine($"Offset {inst.Offset} {prev} => {inst.OpCode} {diff}");

                if ((Instruction)inst.Operand == last)
                {
                    inst.Operand = cleanupInst;
                    Console.WriteLine($"Jump redirect: ret {last.Offset} to End() {cleanupInst.Offset}");
                }
            }

            if (inst.OpCode == OpCodes.Ret && inst != last)
            {
                inst.OpCode = OpCodes.Br;
                inst.Operand = last;
                Console.WriteLine($"Ret redirect: prev ret {inst.Offset} to ret {last.Offset}");
            }
        }

        //Console.WriteLine($"[{method.FullName}]");

        //foreach (var inst in method.Body.Instructions)
        //{
        //    Console.WriteLine(inst);
        //}

        //Console.WriteLine();
    }
}