using System;

namespace HappyToy.V2
{
    // Deterministic independent phases survive checkpoint restoration.
    public static class SchoolFrenzyMotion
    {
        public static float Amount(float elapsed,int index,int family)
        {
            double rate=12.0+((index*7+family*11)%13)*.73;
            double phase=index*2.399963229728653+family*1.37;
            return (float)((1-Math.Cos(Math.Max(0,elapsed)*rate+phase))*.5);
        }
    }
}
