namespace HappyToy.V2
{
    public static class MonsterPassingRules
    {
        public static bool HasRightOfWay(int priority,int identity,int otherPriority,int otherIdentity) =>
            priority<otherPriority || priority==otherPriority && identity<otherIdentity;
        public static bool Passed(bool fromBehind,float along) => fromBehind ? along>.9f : along<-.65f;
    }
}
