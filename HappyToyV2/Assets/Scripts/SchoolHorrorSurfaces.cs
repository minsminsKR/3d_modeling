using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // School-only presentation materials. The original photographed PBR scans
    // and their pool are never modified; this owner releases only its clones.
    public sealed class SchoolHorrorSurfaces : IDisposable
    {
        readonly List<Material> materials=new List<Material>();
        readonly List<Texture2D> textures=new List<Texture2D>();
        readonly Shader shader;
        readonly Texture2D moisture;
        public Material Wall {get;}
        public Material LowerWall {get;}
        public Material Ceiling {get;}
        public Material Floor {get;}
        public Material Glass {get;}
        public Material Rust {get;}
        public Material Timber {get;}
        public Material Puddle {get;}
        public bool Disposed {get;private set;}

        public SchoolHorrorSurfaces(GraphicsSurfaceLibrary.Pool pool)
        {
            if(pool==null)throw new ArgumentNullException(nameof(pool));
            shader=Resources.Load<Shader>("SchoolHorror/SchoolDecaySurface");
            moisture=Texture("moisture-macro");
            if(!shader)throw new InvalidOperationException("School decay PBR shader is missing");
            Wall=Surface("wall",1.8f,new Color(.97f,.97f,.94f),1,.92f,.20f);
            LowerWall=Surface("lower-wall",1.8f,Color.white,1,.92f,.20f);
            Ceiling=Surface("ceiling",2.4f,new Color(.91f,.93f,.92f),.85f,.95f,.27f);
            Floor=Surface("floor",1.28f,Color.white,.88f,.88f,.16f);
            Floor.SetFloat("_WetStrength",1);
            Floor.SetFloat("_WorldFloor",1);
            Floor.SetFloat("_FloorSpan",1.28f);
            Floor.SetVector("_SchoolOrigin",SchoolLoopWorld.Origin);
            Glass=Create("Streaked damp classroom glass",1.6f,"school-glass");
            Glass.SetTexture("_BaseMap",Texture("glass/albedo"));
            Glass.SetTexture("_BumpMap",Texture("glass/normal"));
            Glass.SetTexture("_OcclusionMap",Texture2D.whiteTexture);
            Glass.SetTexture("_MetallicGlossMap",Constant(new Color(0,0,0,.70f),"Glass physical roughness"));
            Glass.SetColor("_BaseColor",Color.white);
            Glass.SetFloat("_BumpScale",.18f);
            Glass.SetFloat("_MacroStrength",.08f);
            Transparent(Glass,1);
            // The architecture uses a closed thin box, so outward entry faces
            // already provide correct views from both sides without double glass.
            Glass.SetFloat("_Cull",(float)CullMode.Back);
            Rust=Clone(pool.Get("metal-rust",new Color(.22f,.20f,.16f)),"Corroded school iron");
            Timber=Clone(pool.Get("wood-aged",new Color(.29f,.28f,.24f)),"Water-darkened school timber");
            Puddle=Create("Shallow irregular school water film",1,"school-water");
            Puddle.SetTexture("_BaseMap",Texture2D.whiteTexture);
            Puddle.SetTexture("_BumpMap",Texture("floor/normal"));
            Puddle.SetTexture("_OcclusionMap",Texture2D.whiteTexture);
            Puddle.SetTexture("_MetallicGlossMap",Constant(new Color(0,0,0,.96f),"Water film physical roughness"));
            Puddle.SetColor("_BaseColor",new Color(.055f,.061f,.057f,.72f));
            Puddle.SetFloat("_BumpScale",.018f);
            Puddle.SetFloat("_WetStrength",1);
            Puddle.SetFloat("_MacroStrength",.12f);
            Transparent(Puddle,2);
        }
        Texture2D Texture(string name)
        {
            var texture=Resources.Load<Texture2D>("SchoolHorror/"+name);
            if(!texture)throw new InvalidOperationException("Required school surface map missing: "+name);
            return texture;
        }
        Texture2D Constant(Color value,string name)
        {
            var texture=new Texture2D(1,1,TextureFormat.RGBA32,false,true){name=name,wrapMode=TextureWrapMode.Repeat};
            texture.SetPixel(0,0,value);texture.Apply(false,true);textures.Add(texture);return texture;
        }
        Material Surface(string key,float metres,Color tint,float normal,float ao,float macro)
        {
            var material=Create("Decayed school "+key,metres,"school-"+key);
            material.SetTexture("_BaseMap",Texture(key+"/albedo"));
            material.SetTexture("_BumpMap",Texture(key+"/normal"));
            material.SetTexture("_OcclusionMap",Texture(key+"/ao"));
            material.SetTexture("_MetallicGlossMap",Texture(key+"/metallic-smoothness"));
            material.SetColor("_BaseColor",tint);
            material.SetFloat("_BumpScale",normal);
            material.SetFloat("_OcclusionStrength",ao);
            material.SetFloat("_MacroStrength",macro);
            return material;
        }
        Material Create(string name,float metres,string key)
        {
            var material=new Material(shader){name=name,enableInstancing=true};
            material.SetTexture("_MoistureMap",moisture);
            material.SetFloat("_Smoothness",1);
            material.SetFloat("_WorldScale",5.7f);
            material.SetVector("_SchoolOrigin",SchoolLoopWorld.Origin);
            material.SetOverrideTag("GraphicsSurface",key);
            material.SetOverrideTag("GraphicsTileMetres",metres.ToString("R",CultureInfo.InvariantCulture));
            materials.Add(material);return material;
        }
        Material Clone(Material original,string name)
        {
            var material=new Material(original){name=name,enableInstancing=true};
            materials.Add(material);return material;
        }
        static void Transparent(Material material,int mode)
        {
            material.SetFloat("_SurfaceMode",mode);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetFloat("_SrcBlend",(float)BlendMode.One);
            material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0);
            material.SetFloat("_Cull",(float)CullMode.Off);
            material.SetOverrideTag("RenderType","Transparent");
            material.renderQueue=(int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster",false);
            material.SetShaderPassEnabled("DepthOnly",false);
            material.SetShaderPassEnabled("DepthNormals",false);
        }
        public void Dispose()
        {
            if(Disposed)return;Disposed=true;
            foreach(var material in materials)GraphicsSurfaceLibrary.DestroyOwned(material);
            foreach(var texture in textures)GraphicsSurfaceLibrary.DestroyOwned(texture);
            materials.Clear();textures.Clear();
        }
    }
}
