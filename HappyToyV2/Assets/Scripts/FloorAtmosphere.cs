using UnityEngine;

namespace HappyToy.V2
{
    public sealed class FloorAtmosphere : MonoBehaviour
    {
        Color originalFog;
        float originalDensity;
        AudioClip waterClip;
        AudioSource waterSound;
        void Start()
        {
            originalFog=RenderSettings.fogColor;originalDensity=RenderSettings.fogDensity;
            var emitter=new GameObject("Basement distant water drops");emitter.transform.SetParent(transform);emitter.transform.position=new Vector3(13.8f,-3,-29);
            waterSound=emitter.AddComponent<AudioSource>();waterSound.spatialBlend=1;waterSound.minDistance=3;waterSound.maxDistance=19;waterSound.loop=true;waterSound.volume=.35f;
            const int rate=24000;var samples=new float[rate*8];var random=new System.Random(441);
            for(int k=0;k<12;k++)
            {int start=(int)((.2f+k*.63f)*rate);float frequency=700+(float)random.NextDouble()*900;
                for(int i=0;i<4500&&start+i<samples.Length;i++){float t=i/(float)rate;samples[start+i]+=.23f*Mathf.Exp(-24*t)*Mathf.Sin(2*Mathf.PI*(frequency*t-650*t*t));}}
            waterClip=AudioClip.Create("Basement irregular drips",samples.Length,1,rate,false);waterClip.SetData(samples,0);waterSound.clip=waterClip;waterSound.Play();
        }
        void LateUpdate()
        {
            if(!GameSession.Current)return;
            float y=GameSession.Current.player.transform.position.y;
            Color fog=y< -2?new Color(.018f,.045f,.045f):y>3?new Color(.085f,.008f,.012f):originalFog;
            RenderSettings.fogColor=Color.Lerp(RenderSettings.fogColor,fog,Time.unscaledDeltaTime*2);
            RenderSettings.fogDensity=Mathf.Lerp(RenderSettings.fogDensity,y< -2?.038f:y>3?.032f:originalDensity,Time.unscaledDeltaTime*2);
        }
        void OnDestroy(){RenderSettings.fogColor=originalFog;RenderSettings.fogDensity=originalDensity;if(waterClip)Destroy(waterClip);}
    }
}
