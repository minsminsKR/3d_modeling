using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace HappyToy.V2.CloudTests
{
    public sealed class ChapterCheckpointStoreTests
    {
        string directory,path;
        Type storeType,dataType;
        [SetUp] public void Setup()
        {
            directory=Path.GetFullPath(Path.Combine("Temp","ChapterSlotTests",Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory); path=Path.Combine(directory,"chapter-suspend-v1.json");
            storeType=RuntimeAccess.RequireType("ChapterCheckpointStore"); dataType=RuntimeAccess.RequireType("ChapterCheckpoint");
        }
        [TearDown] public void Cleanup()
        {
            var allowed=Path.GetFullPath(Path.Combine("Temp","ChapterSlotTests"))+Path.DirectorySeparatorChar;
            Assert.That(directory.StartsWith(allowed,StringComparison.OrdinalIgnoreCase),Is.True);
            if(Directory.Exists(directory)) Directory.Delete(directory,true);
        }
        object Store(bool write=true) => Activator.CreateInstance(storeType,directory,write);
        object Data()
        {
            const string json="{\"scene\":\"Assets/Annex/SchoolAnnex.unity\",\"recovered\":0,\"seconds\":17,\"player\":{\"position\":{\"x\":-7.8,\"y\":0.02},\"stamina\":0.7,\"stock\":2},\"cyclopse\":{\"door\":\"\"},\"portraitActor\":{\"door\":\"\"},\"nurseryActor\":{\"door\":\"\"},\"mannequin\":{\"rotation\":{\"w\":1},\"visualRotation\":{\"w\":1}},\"mask\":{\"rotation\":{\"w\":1}},\"doors\":[],\"supplies\":[]}";
            string old=json.Insert(1,"\"version\":1,\"simulationVersion\":1,\"token\":\""+Guid.NewGuid().ToString("N")+"\",");
            var data=RuntimeAccess.Call(dataType,"FromJson",old);
            RuntimeAccess.Set(data,"version",1); RuntimeAccess.Set(data,"simulationVersion",1);
            RuntimeAccess.Set(data,"token",Guid.NewGuid().ToString("N")); return data;
        }
        [Test] public void RawVersionOneSchoolSaveWithoutMetricsLoadsAsAbsentAndRemainsWritable()
        {
            string old=(string)RuntimeAccess.Call(Data(),"ToJson"); Assert.That(old,Does.Not.Contain("\"runProgress\""));
            File.WriteAllText(path,old); var store=Store(); Assert.That(RuntimeAccess.Get<bool>(store,"HasRun"),Is.True);
            var data=RuntimeAccess.Get<object>(store,"Snapshot"); Assert.That(RuntimeAccess.Get<object>(data,"runProgress"),Is.Null);
            RuntimeAccess.Call(data,"Validate"); Assert.That(Save(store,data),Is.True);
            Assert.That(File.ReadAllText(path),Does.Not.Contain("\"runProgress\""));
            Assert.That(RuntimeAccess.Get<object>(RuntimeAccess.Get<object>(Store(),"Snapshot"),"runProgress"),Is.Null);
        }
        [Test] public void ExplicitMalformedMetricsAreRejectedAndTheirRawBytesArePreserved()
        {
            string old=(string)RuntimeAccess.Call(Data(),"ToJson");
            foreach(var metrics in new[]{"{}","{\"token\":\"bad\",\"throws\":-1}"})
            {
                string invalid=old.Insert(old.LastIndexOf('}'),",\"runProgress\":"+metrics);
                File.WriteAllText(path,invalid); var store=Store(); Assert.That(RuntimeAccess.Get<bool>(store,"Writable"),Is.False);
                Assert.That(Save(store,Data()),Is.False); Assert.That(File.ReadAllText(path),Is.EqualTo(invalid));
            }
        }
        bool Save(object store,object data) => (bool)RuntimeAccess.Call(store,"Save",data);
        [Test] public void ChapterRoundTripKeepsSchoolResourcesAndConsumesOnlyOnce()
        {
            var store=Store(); var data=Data(); Assert.That(Save(store,data),Is.True);
            RuntimeAccess.Set(RuntimeAccess.Get<object>(data,"player"),"stock",0);
            Assert.That(RuntimeAccess.Get<int>(RuntimeAccess.Get<object>(RuntimeAccess.Get<object>(store,"Snapshot"),"player"),"stock"),Is.EqualTo(2));
            var reader=Store(); var token=RuntimeAccess.Get<string>(RuntimeAccess.Get<object>(reader,"Snapshot"),"token");
            Assert.That((bool)RuntimeAccess.Call(reader,"Consume",token),Is.True);
            Assert.That((bool)RuntimeAccess.Call(reader,"Consume",token),Is.False);
            Assert.That(RuntimeAccess.Get<bool>(Store(),"HasRun"),Is.False); Assert.That(File.Exists(path+".used"),Is.True);
            Assert.That(File.Exists(Path.Combine(directory,"corridor-suspend-v1.json")),Is.False);
        }
        [Test] public void SchoolAndCorridorSchemasRemainSeparateAndInvalidProgressNeverCommits()
        {
            var school=Data(); RuntimeAccess.Call(school,"Validate");
            var player=RuntimeAccess.Get<object>(school,"player");
            Assert.Throws<ArgumentException>(()=>RuntimeAccess.Call(player,"Validate"));
            RuntimeAccess.Call(player,"ValidateChapter");
            RuntimeAccess.Set(player,"position",new Vector3(200,.02f,200));
            RuntimeAccess.Call(player,"Validate"); Assert.That(Save(Store(),school),Is.False);
            var impossible=Data(); RuntimeAccess.Set(impossible,"recovered",5);
            Assert.That(Save(Store(),impossible),Is.False); Assert.That(File.Exists(path),Is.False);
        }
        [Test] public void FailedLockedAndStaleWritesPreserveExistingRun()
        {
            var first=Store(); var stale=Store(); var a=Data(); Assert.That(Save(first,a),Is.True);
            Assert.That(Save(stale,Data()),Is.False);
            var reader=Store(); Assert.That(Save(first,Data()),Is.True);
            Assert.That((bool)RuntimeAccess.Call(reader,"Consume",RuntimeAccess.Get<string>(a,"token")),Is.False);
            Assert.That(RuntimeAccess.Get<bool>(reader,"Conflict"),Is.True);
            var current=File.ReadAllText(path);
            using(var lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                Assert.That(Save(first,Data()),Is.False);
            Assert.That(File.ReadAllText(path),Is.EqualTo(current)); Assert.That(Save(Store(false),Data()),Is.False);
            Assert.That(Directory.GetFiles(directory,"*.tmp-*").Length,Is.Zero);
        }
        [Test] public void UnknownCorruptOversizedAndCorridorFilesArePreserved()
        {
            foreach(var bytes in new[]{"{\"version\":999}","not json",new string('x',128*1024+1),
                File.ReadAllText("Assets/Tests/Fixtures/corridor-checkpoint-state.json")})
            {
                File.WriteAllText(path,bytes); var store=Store();
                Assert.That(RuntimeAccess.Get<bool>(store,"Writable"),Is.False);
                Assert.That(Save(store,Data()),Is.False); Assert.That(File.ReadAllText(path),Is.EqualTo(bytes));
            }
        }
    }
}
