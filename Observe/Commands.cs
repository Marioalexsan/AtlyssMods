using System.Runtime.CompilerServices;
using AtlyssCommandLib.API;
using UnityEngine;

namespace Marioalexsan.Observe;

internal static class Commands
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    internal static void RegisterCommands()
    {
        var observeRoot = new CommandProvider("observe", "Observe commands");
        CommandProvider.Root.RegisterProvider(observeRoot);

        var reset = observeRoot.RegisterCommand("default", "Look where your camera is looking", (caller, args) => LookCommand("default", caller, args));
        observeRoot.RegisterAlias("reset", reset);
        observeRoot.RegisterCommand("left", "Look to your left", (caller, args) => LookCommand("left", caller, args));
        observeRoot.RegisterCommand("right", "Look to your right", (caller, args) => LookCommand("right", caller, args));
        observeRoot.RegisterCommand("up", "Look up", (caller, args) => LookCommand("up", caller, args));
        observeRoot.RegisterCommand("down", "Look down", (caller, args) => LookCommand("down", caller, args));
        observeRoot.RegisterCommand("forward", "Look forward", (caller, args) => LookCommand("forward", caller, args));
        observeRoot.RegisterCommand("backward", "Look backward (might require Owl mode)", (caller, args) => LookCommand("backward", caller, args));
        observeRoot.RegisterCommand("camera", "Look at the camera", (caller, args) => LookCommand("camera", caller, args));
        observeRoot.RegisterCommand("pose", "Look relative to your current camera direction", (caller, args) => LookCommand("pose", caller, args));
        observeRoot.RegisterCommand("freeze", "Freeze your current look direction", (caller, args) => LookCommand("freeze", caller, args));
        observeRoot.RegisterCommand("environment", "Look in the far distance based on your current camera direction", (caller, args) => LookCommand("environment", caller, args));
        observeRoot.RegisterCommand("owl", "Toggles Owl mode for yourself", ToggleOwl);
        observeRoot.RegisterCommand("vanilla", "Toggles Vanilla Mode for yourself", ToggleVanilla);
        observeRoot.RegisterCommand("tilt", "Tilt your head left or right by the given angle", SetHeadTilt);
        observeRoot.RegisterCommand("speed", "Toggles look speed for yourself", $"Available options are {string.Join(", ", Enum.GetNames(typeof(LookSpeed)))}.", ToggleLookSpeed);
        observeRoot.RegisterCommand("frontal", "Toggles behaviour of frontal angles for Default mode", $"Available options are {string.Join(", ", Enum.GetNames(typeof(BackwardLookMode)))}.", ChangeFrontalAngleMode);
    }

    private static bool ToggleOwl(Caller caller, string[] args)
    {
        if (args.Length > 0)
            return false;

        var newValue = !ObservePlugin.OwlModeSetting.Value;

        ObservePlugin.OwlModeSetting.Value = newValue;
        Utils.NotifyCaller(caller, $"Owl Mode is now {(newValue ? "on" : "off")}.");
        
        return true;
    }

    private static bool ToggleVanilla(Caller caller, string[] args)
    {
        if (args.Length > 0)
            return false;

        var newValue = !ObservePlugin.VanillaModeSetting.Value;

        ObservePlugin.VanillaModeSetting.Value = newValue;
        Utils.NotifyCaller(caller, $"Vanilla Mode is now {(newValue ? "on" : "off")}.");
        
        return true;
    }

    private static bool ToggleLookSpeed(Caller caller, string[] args)
    {
        if (args.Length != 1)
            return false;

        if (!Enum.TryParse<LookSpeed>(args[0], true, out var value))
            return false;

        ObservePlugin.LookSpeedSetting.Value = value;
        Utils.NotifyCaller(caller, $"Look speed is now {value.ToString()}.");
        
        return true;
    }

    private static bool ChangeFrontalAngleMode(Caller caller, string[] args)
    {
        if (args.Length != 1)
            return false;

        if (!Enum.TryParse<BackwardLookMode>(args[0], true, out var value))
            return false;

        ObservePlugin.BackwardLookModeSetting.Value = value;
        Utils.NotifyCaller(caller, $"Frontal angle mode is now {value.ToString()}.");
        
        return true;
    }

    private static bool LookCommand(string lookDirection, Caller caller, string[] args)
    {
        if (args.Length > 1)
            return false;

        switch (lookDirection)
        {
            case "left":
                ObservePlugin.SavedOverride = Quaternion.Euler(0, -90, 0);
                ObservePlugin.LocalOverrideDirection = LookDirection.Pose;
                break;
            case "right":
                ObservePlugin.SavedOverride = Quaternion.Euler(0, 90, 0);
                ObservePlugin.LocalOverrideDirection = LookDirection.Pose;
                break;
            case "up":
                ObservePlugin.SavedOverride = Quaternion.Euler(-80, 0, 0); // Full 90 would cause issues
                ObservePlugin.LocalOverrideDirection = LookDirection.Pose;
                break;
            case "down":
                ObservePlugin.SavedOverride = Quaternion.Euler(80, 0, 0); // Full 90 would cause issues
                ObservePlugin.LocalOverrideDirection = LookDirection.Pose;
                break;
            case "forward":
                ObservePlugin.SavedOverride = Quaternion.identity;
                ObservePlugin.LocalOverrideDirection = LookDirection.Pose;
                break;
            case "backward":
                ObservePlugin.SavedOverride = Quaternion.Euler(0, 180, 0);
                ObservePlugin.LocalOverrideDirection = LookDirection.Pose;
                break;
            case "pose":
                ObservePlugin.SavedOverride = Quaternion.Inverse(Player._mainPlayer.transform.rotation) * CameraFunction._current.transform.rotation;
                ObservePlugin.LocalOverrideDirection = LookDirection.Pose;
                break;
            case "freeze":
                var headBone = ObservePlugin.GetHeadBone(Player._mainPlayer.GetComponentInChildren<PlayerRaceModel>());
                ObservePlugin.SavedOverride = Quaternion.Inverse(Player._mainPlayer.transform.rotation) * headBone!.rotation;
                ObservePlugin.LocalOverrideDirection = LookDirection.Pose;
                break;
            case "environment":
                ObservePlugin.SavedOverride = CameraFunction._current.transform.rotation;
                ObservePlugin.LocalOverrideDirection = LookDirection.Environment;
                break;
            case "camera":
                ObservePlugin.LocalOverrideDirection = LookDirection.Camera;
                break;
            default:
                ObservePlugin.LocalOverrideDirection = LookDirection.Default;
                break;
        }

        if (args.Length == 1)
        {
            if (!int.TryParse(args[0], out var durationSeconds))
            {
                Utils.NotifyCaller(caller, $"\"{args[0]}\" is not right! It should be a valid duration in seconds!", Color.red);
                return false;
            }
            
            ObservePlugin.LocalOverrideDirectionTime = TimeSpan.FromSeconds(durationSeconds);
        }
        else
        {
            ObservePlugin.LocalOverrideDirectionTime = TimeSpan.FromDays(300);
        }
        
        return true;
    }

    private static bool SetHeadTilt(Caller caller, string[] args)
    {
        if (args.Length > 2 || args.Length == 0)
            return false;

        int angle = 18;
        const int MaxAngle = 35;

        if (args.Length == 2)
        {
            if (!int.TryParse(args[1], out angle))
            {
                Utils.NotifyCaller(caller, $"\"{args[0]}\" is not right! It should be a valid angle in degrees!");
                return false;
            }
            
            if (angle < 0 || angle > MaxAngle)
            {
                Utils.NotifyCaller(caller, $"\"{args[0]}\" needs to be between 0 and {MaxAngle} degrees!");
                return false;
            }
        }
        
        switch (args[0])
        {
            case "left":
                ObservePlugin.SavedTilt = Quaternion.Euler(0, 0, angle);
                break;
            case "right":
                ObservePlugin.SavedTilt = Quaternion.Euler(0, 0, -angle);
                break;
            default:
                ObservePlugin.SavedTilt = Quaternion.identity;
                break;
        }

        return true;
    }
}