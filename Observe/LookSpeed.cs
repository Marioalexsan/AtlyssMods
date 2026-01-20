namespace Marioalexsan.Observe;

public enum LookSpeed : byte
{
    Sloth = 1,
    VerySlow = 2,
    Slow = 3,
    Normal = 0,
    Fast = 4,
    VeryFast = 5,
    Caffeinated = 6,
}

public static class LookSpeedExtensions
{
    public static float MapToMultiplier(this LookSpeed speed) => speed switch
    {
        LookSpeed.Sloth => 0.5f,
        LookSpeed.VerySlow => 1.5f,
        LookSpeed.Slow => 2.5f,
        LookSpeed.Normal => 3.5f,
        LookSpeed.Fast => 5.5f,
        LookSpeed.VeryFast => 7.5f,
        LookSpeed.Caffeinated => 18f,
        _ => 3.5f
    };
}