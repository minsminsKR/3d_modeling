using System;

static class ClockTestProgram
{
    static int Main()
    {
        try
        {
            int count = HappyToy.V2.Editor.EnemyAttackClockChecks.Run();
            Console.WriteLine("EnemyAttackClock: " + count + " assertions passed against the actual C# helper.");
            int rippleCount = HappyToy.V2.Editor.SurfaceRippleChecks.Run();
            Console.WriteLine("SurfaceRippleBuffer: " + rippleCount + " assertions passed against the actual C# helper.");
            int stealthCount = HappyToy.V2.Editor.StealthRulesChecks.Run();
            Console.WriteLine("StealthRules: " + stealthCount + " assertions passed against the actual C# helper.");
            int restartCount = HappyToy.V2.Editor.SceneRestartGateChecks.Run();
            Console.WriteLine("SceneRestartGate: " + restartCount + " assertions passed against the actual C# helper.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
