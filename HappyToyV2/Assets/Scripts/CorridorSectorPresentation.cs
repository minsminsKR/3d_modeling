using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    public sealed partial class HauntedCorridorPresentation
    {
        static readonly string[] sectorNames={"그을음","습기","잿빛","붉은 실","먼지"};
        static readonly Color[] paperTints={new Color(.65f,.61f,.52f),new Color(.68f,.72f,.62f),
            new Color(.72f,.70f,.61f),new Color(.77f,.65f,.54f),new Color(.80f,.75f,.60f)};
        static readonly Color[] bindingTints={new Color(.20f,.17f,.13f),new Color(.22f,.30f,.24f),
            new Color(.39f,.36f,.28f),new Color(.38f,.12f,.075f),new Color(.50f,.43f,.29f)};
        static readonly float[] bayWidths={.74f,.94f,.62f,1.02f,.82f};
        static readonly float[][] crossbarHeights={new[]{1.27f,2.00f,2.32f},new[]{1.15f,1.73f,2.28f},
            new[]{1.45f,2.12f,2.38f},new[]{1.21f,1.95f,2.44f},new[]{1.35f,1.84f,2.22f}};
        readonly Material[] sectorPaper=new Material[5], sectorBinding=new Material[5];
        readonly List<TextMesh> sectorClues=new List<TextMesh>();
        readonly List<(TextMesh text,float width,float height)> fittedSectorClues=new List<(TextMesh,float,float)>();
        public CorridorSectorPlan SectorPlan { get; private set; }
        public IReadOnlyList<TextMesh> SectorClues => sectorClues;
        public int GoalRoomClues { get; private set; }
        public int BranchClues { get; private set; }
        public Material SectorPaper(int cell) => sectorPaper[SectorPlan.Sectors[cell]];
        Material SectorBinding(int cell) => sectorBinding[SectorPlan.Sectors[cell]];
        float SectorBayWidth(int cell) => bayWidths[SectorPlan.Sectors[cell]];
        float[] SectorCrossbars(int cell) => crossbarHeights[SectorPlan.Sectors[cell]];

        void PrepareSectors()
        {
            SectorPlan=new CorridorSectorPlan(run.Layout);
            for(int i=0;i<5;i++)
            {
                var texture=SectorPaperTexture(i);
                sectorPaper[i]=Surface("Sector "+i+" aged paper — "+sectorNames[i],paperTints[i],texture);
                sectorBinding[i]=Surface("Sector "+i+" worn binding — "+sectorNames[i],bindingTints[i],texture);
            }
        }
        Texture2D SectorPaperTexture(int sector)
        {
            // Sector differences belong to coherent pigment/dampness and joinery,
            // not a different random stretched checker for every room.
            return PaperTexture();
        }
        void DressSectorClues()
        {
            for(int cell=0;cell<CorridorLayout.Count;cell++)
            {
                int goal=Array.IndexOf(SectorPlan.GoalCells,cell);
                int branch=SectorPlan.BranchOrdinals[cell];
                // Goal identities are permanent; numbered branches are sparse, real topology marks.
                if(goal<0 && branch==0) continue;
                int direction=ChooseClueWall(cell,out float offset);
                Vector3 outward=new Vector3(CorridorLayout.DX[direction],0,CorridorLayout.DZ[direction]);
                Vector3 inward=-outward,across=new Vector3(outward.z,0,-outward.x);
                Vector3 center=run.CellPosition(cell);center.y=0;
                Vector3 plane=center+outward*3+across*offset+inward*.183f+Vector3.up*1.73f;
                Quaternion basis=Quaternion.LookRotation(inward);
                var board=CellDraft(cell,SectorPaper(cell),false);var frame=CellDraft(cell,timber,false);
                float width=goal>=0?.92f:.68f,height=goal>=0?.45f:.36f;
                board.Box(plane,new Vector3(width,height,.013f),basis);
                // Four fine joinery edges stay flat on an already opaque wall.
                foreach(float side in new[]{-1f,1f})
                {
                    frame.Box(plane+across*side*(width*.5f+.015f),new Vector3(.021f,height+.044f,.019f),basis);
                    frame.Box(plane+Vector3.up*side*(height*.5f+.015f),new Vector3(width+.04f,.021f,.019f),basis);
                }
                var stamp=CellDraft(cell,SectorBinding(cell),false);
                // Distinct quiet cord marks, rather than colour-only navigation.
                int strokes=goal>=0?goal+1:CorridorSectorPlan.Degree(SectorPlan.Connections[cell]);
                for(int i=0;i<strokes;i++)
                    stamp.Box(plane+across*((i-(strokes-1)*.5f)*.066f)+inward*.015f-Vector3.up*(height*.5f-.055f),
                        new Vector3(.032f,.035f,.004f),basis);
                string label=goal>=0?"봉인 "+(goal+1).ToString("D2")+" · "+sectorNames[goal]:
                    "갈림 "+branch.ToString("D2")+" · "+CorridorSectorPlan.Degree(SectorPlan.Connections[cell])+"갈래";
                var text=new GameObject((goal>=0?"Goal chamber clue ":"Branch topology clue ")+cell).AddComponent<TextMesh>();
                text.transform.SetParent(additions,false);
                // TextMesh front points opposite local Z; this normal faces the actual room.
                text.transform.SetPositionAndRotation(plane+inward*.017f,Quaternion.LookRotation(outward));
                text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=72;
                text.characterSize=goal>=0?.014f:.012f;text.color=new Color(.13f,.10f,.067f);
                text.text=label;text.gameObject.AddComponent<AnnexSignFont>().Apply();
                sectorClues.Add(text);fittedSectorClues.Add((text,width*.91f,height*.64f));
                if(goal>=0) GoalRoomClues++; else BranchClues++;
            }
        }
        int ChooseClueWall(int cell,out float offset)
        {
            // Prefer the solid end of a sightline. Four-way intersections have actual solid
            // doorway side walls at +/-2.18m; their plaque never enters the 2.58m clear opening.
            int first=unchecked(run.Seed+cell*13)&3;
            for(int i=0;i<4;i++)
            { int d=(first+i)%4;if((SectorPlan.Connections[cell]&(1<<d))==0) {offset=0;return d;} }
            offset=2.18f;return first;
        }
        void FitSectorClues()
        {
            foreach(var clue in fittedSectorClues)
            {
                if(!clue.text) continue;
                var bounds=clue.text.GetComponent<MeshRenderer>().bounds;
                // Plaques use cardinal rotations only; projected horizontal span is exact.
                float width=Mathf.Max(bounds.size.x,bounds.size.z);
                float fit=Mathf.Max(width/clue.width,bounds.size.y/clue.height);
                if(fit>1.001f) clue.text.transform.localScale/=fit;
            }
        }
    }
}
