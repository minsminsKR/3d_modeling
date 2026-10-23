using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Visual-only music room art. Shared imported meshes/PBR stay Resources-owned.
    public sealed class SchoolMusicRoomDressing
    {
        readonly SchoolLoopWorld world;
        public SchoolMusicRoomDressing(SchoolLoopWorld owner){world=owner;}
        Transform Prop(Transform parent,string key,Vector3 at,float height,float yaw)
        {
            if(key=="classroom-chair")
            {
                var chair=SchoolRenewalModels.Source("school-chair");
                var size=GraphicsPropLibrary.LocalBounds(chair.transform).size;
                size*=height/Mathf.Max(.001f,size.y);
                var seating=SchoolRenewalModels.AtFeet("school-chair",parent,at,size,yaw);
                seating.name="Music room classroom-chair";
                return seating;
            }
            var prefab=Resources.Load<GameObject>("SchoolLoop/"+key+"-visual");
            if(!prefab)throw new InvalidOperationException("Required school music prop missing: "+key);
            var holder=new GameObject("Music room "+key).transform;holder.SetParent(parent,false);
            holder.localPosition=at;holder.localRotation=Quaternion.Euler(0,yaw,0);
            var model=UnityEngine.Object.Instantiate(prefab,holder,false).transform;
            var bounds=GraphicsPropLibrary.LocalBounds(holder);
            float scale=height/Mathf.Max(.001f,bounds.size.y);
            model.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            holder.localScale=Vector3.one*scale;
            if(holder.GetComponentsInChildren<Collider>(true).Length!=0)throw new InvalidOperationException("Music art must not add room access/physics");
            return holder;
        }
        public void Build(Transform chunk,bool centre)
        {
            var root=new GameObject("Detailed school music room").transform;root.SetParent(chunk,false);
            if(centre)world.MusicRoom=root;
            Prop(root,"grand-piano",new Vector3(-6.25f,0,31.3f),1.75f,112);
            // Two banks face the piano; the clear middle aisle makes the room readable through the glazing.
            for(int row=0;row<3;row++)for(int col=0;col<4;col++)
            {
                float x=new[]{-8.2f,-7.05f,-4.85f,-3.7f}[col];
                Prop(root,"classroom-chair",new Vector3(x,0,26.3f+row*1.12f),.82f,180);
                if(row<2&&col%2==0)Prop(root,"music-stand",new Vector3(x+.15f,0,26.78f+row*1.12f),1.16f,180);
            }
            Prop(root,"music-stand",new Vector3(-8.1f,0,30.3f),1.35f,45);
            var board=world.Surfaces.Get("painted-metal",new Color(.15f,.24f,.20f));
            var wood=world.Surfaces.Get("wood-aged",new Color(.52f,.40f,.25f));
            var chalk=world.Surfaces.Get("paper-aged",new Color(.72f,.73f,.62f));
            world.Box(root,"Music blackboard timber surround",new Vector3(-6.0f,1.7f,33.25f),new Vector3(4.1f,1.25f,.12f),wood);
            world.Box(root,"Music classroom chalk board",new Vector3(-6,1.7f,33.17f),new Vector3(3.94f,1.1f,.025f),board);
            for(int stave=0;stave<2;stave++)for(int line=0;line<5;line++)
                world.Box(root,"Chalk staff line",new Vector3(-6,1.55f+stave*.36f+line*.032f,33.149f),new Vector3(3.42f,.006f,.006f),chalk);
            world.Label(root,"음악",new Vector3(-7.39f,2.12f,33.135f),Quaternion.identity,new Vector2(.55f,.19f),new Color(.77f,.78f,.69f));
            // Fixed timber acoustic treatment and existing wooden seating complement the two generated hero assets.
            for(int i=0;i<2;i++)world.Box(root,"Music wall acoustic timber slat",new Vector3(-9.20f,1.7f,27.4f+i*2.6f),new Vector3(.055f,1.75f,.48f),wood);
            for(int i=0;i<3;i++)
            {
                float z=26.1f+i*2.6f;
                world.Box(root,"Music exterior window dark recess",new Vector3(-9.285f,1.84f,z),new Vector3(.035f,1.48f,1.65f),world.Surfaces.Get("painted-metal",new Color(.06f,.08f,.085f)));
                SchoolRenewalModels.Attach("classroom-window",root,
                    new Bounds(new Vector3(-9.20f,1.84f,z),new Vector3(1.65f,1.6f,.08f)),-90);
            }
            foreach(float x in new[]{-7.7f,-4.3f})foreach(float z in new[]{27.2f,31.0f})world.Fixture(root,new Vector3(x,3.08f,z),true,3.0f);
        }
    }
}
