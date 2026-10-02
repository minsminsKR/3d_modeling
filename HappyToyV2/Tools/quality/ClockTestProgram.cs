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
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
