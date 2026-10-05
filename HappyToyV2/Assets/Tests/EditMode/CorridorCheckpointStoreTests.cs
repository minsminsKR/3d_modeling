using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HappyToy.V2.CloudTests
{
    public sealed class CorridorCheckpointStoreTests
    {
        string directory,path;
        Type storeType,dataType;
        [SetUp] public void Setup()
        {
            directory=Path.GetFullPath(Path.Combine("Temp","CorridorSlotTests",Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory); path=Path.Combine(directory,"corridor-suspend-v1.json");
            storeType=RuntimeAccess.RequireType("CorridorCheckpointStore"); dataType=RuntimeAccess.RequireType("CorridorCheckpoint");
        }
        [TearDown] public void Cleanup()
        {
            var allowed=Path.GetFullPath(Path.Combine("Temp","CorridorSlotTests"))+Path.DirectorySeparatorChar;
            Assert.That(directory.StartsWith(allowed,StringComparison.OrdinalIgnoreCase),Is.True);
            if(Directory.Exists(directory)) Directory.Delete(directory,true);
        }
        object Store(bool write=true) => Activator.CreateInstance(storeType,directory,write);
        object Data()
        {
            var data=JsonUtility.FromJson(File.ReadAllText("Assets/Tests/Fixtures/corridor-checkpoint-state.json"),dataType);
            RuntimeAccess.Set(data,"token",Guid.NewGuid().ToString("N")); return data;
        }
        bool Save(object store,object data) => (bool)RuntimeAccess.Call(store,"Save",data);
        string ConsumeDiagnostic(object store) => "Status="+RuntimeAccess.Get<string>(store,"Status")+
            "; Conflict="+RuntimeAccess.Get<bool>(store,"Conflict")+"; "+RuntimeAccess.Get<string>(store,"StorageDiagnostic");
        [Test] public void RoundTripIsImmutableAndConsumeCannotReviveOldBackups()
        {
            var store=Store(); var data=Data(); Assert.That(Save(store,data),Is.True);
            var token=RuntimeAccess.Get<string>(data,"token");
            RuntimeAccess.Set(RuntimeAccess.Get<object>(data,"player"),"stock",0);
            Assert.That(RuntimeAccess.Get<int>(RuntimeAccess.Get<object>(RuntimeAccess.Get<object>(store,"Snapshot"),"player"),"stock"),Is.EqualTo(2));
            var reloaded=Store(); Assert.That(RuntimeAccess.Get<bool>(reloaded,"HasRun"),Is.True);
            Assert.That((bool)RuntimeAccess.Call(reloaded,"Consume",token),Is.True);
            Assert.That((bool)RuntimeAccess.Call(reloaded,"Consume",token),Is.False);
            Assert.That(RuntimeAccess.Get<bool>(Store(),"HasRun"),Is.False);
            Assert.That(File.Exists(path+".used"),Is.True);
            var next=Data(); Assert.That(Save(reloaded,next),Is.True); Assert.That(Save(reloaded,Data()),Is.True);
            Assert.That(File.Exists(path+".bak"),Is.True);
            Assert.That((bool)RuntimeAccess.Call(reloaded,"Consume",RuntimeAccess.Get<string>(RuntimeAccess.Get<object>(reloaded,"Snapshot"),"token")),Is.True,ConsumeDiagnostic(reloaded));
            Assert.That(RuntimeAccess.Get<bool>(Store(),"HasRun"),Is.False,"Consumed slot revived a .bak");
        }
        [Test] public void LockedConsumedArchivePreservesLiveSnapshotAndRetriesAfterUnlock()
        {
            if(Application.platform!=RuntimePlatform.WindowsEditor)
                Assert.Ignore("This archive sharing case verifies Windows FileShare.None replacement semantics.");
            var store=Store(); var first=Data(); Assert.That(Save(store,first),Is.True);
            Assert.That((bool)RuntimeAccess.Call(store,"Consume",RuntimeAccess.Get<string>(first,"token")),Is.True,ConsumeDiagnostic(store));
            string oldUsed=File.ReadAllText(path+".used");
            Assert.That(Save(store,Data()),Is.True); Assert.That(Save(store,Data()),Is.True);
            string primary=File.ReadAllText(path),backup=File.ReadAllText(path+".bak");
            string token=RuntimeAccess.Get<string>(RuntimeAccess.Get<object>(store,"Snapshot"),"token");
            using(var archiveLease=new FileStream(path+".used",FileMode.Open,FileAccess.Read,FileShare.None))
            {
                Assert.That((bool)RuntimeAccess.Call(store,"Consume",token),Is.False);
                Assert.That(RuntimeAccess.Get<string>(store,"StorageDiagnostic"),Does.Contain("phase=replace-used"));
                Assert.That(RuntimeAccess.Get<string>(store,"StorageDiagnostic"),Does.Contain("System.IO.IOException"));
                Assert.That(RuntimeAccess.Get<string>(store,"StorageDiagnostic"),Does.Contain("HResult=0x80070020"));
                Assert.That(RuntimeAccess.Get<bool>(store,"Conflict"),Is.False,"A locked archive is not a stale-token conflict");
                Assert.That(File.ReadAllText(path),Is.EqualTo(primary)); Assert.That(File.ReadAllText(path+".bak"),Is.EqualTo(backup));
                Assert.That(RuntimeAccess.Get<bool>(store,"HasRun"),Is.True);
                Assert.That(RuntimeAccess.Get<string>(RuntimeAccess.Get<object>(store,"Snapshot"),"token"),Is.EqualTo(token));
            }
            Assert.That(File.ReadAllText(path+".used"),Is.EqualTo(oldUsed));
            Assert.That((bool)RuntimeAccess.Call(store,"Consume",token),Is.True,ConsumeDiagnostic(store));
            Assert.That(RuntimeAccess.Get<string>(store,"StorageDiagnostic"),Is.Empty);
            Assert.That(File.Exists(path),Is.False); Assert.That(File.ReadAllText(path+".used"),Is.EqualTo(primary));
            Assert.That(File.ReadAllText(path+".bak"),Is.EqualTo(backup));
            Assert.That(RuntimeAccess.Get<bool>(Store(),"HasRun"),Is.False,"Consumed archive/backup must never become a readable live slot");
        }
        [Test] public void UnknownCorruptAndOversizedProfilesArePreserved()
        {
            foreach(var bytes in new[] {"{\"version\":999}","not valid json",new string('x',128*1024+1)})
            {
                File.WriteAllText(path,bytes); var store=Store(); Assert.That(RuntimeAccess.Get<bool>(store,"Writable"),Is.False);
                Assert.That(Save(store,Data()),Is.False); Assert.That(File.ReadAllText(path),Is.EqualTo(bytes));
            }
        }
        [Test] public void LockReadOnlyAndInvalidDataNeverCommitAndRetryWorks()
        {
            var store=Store(); var data=Data();
            Assert.That(Save(Store(false),data),Is.False);
            var invalid=Data(); RuntimeAccess.Set(RuntimeAccess.Get<object>(invalid,"player"),"stamina",float.NaN);
            Assert.That(Save(store,invalid),Is.False); Assert.That(Save(store,null),Is.False);
            using(var lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                Assert.That(Save(store,data),Is.False);
            Assert.That(File.Exists(path),Is.False); Assert.That(RuntimeAccess.Get<bool>(store,"HasRun"),Is.False);
            Assert.That(Save(store,data),Is.True); Assert.That(Directory.GetFiles(directory,"*.tmp-*").Length,Is.Zero);
        }
        [Test] public void StaleInstancesCannotOverwriteOrConsumeAnotherRun()
        {
            var first=Store(); var stale=Store(); var a=Data(); var b=Data();
            Assert.That(Save(first,a),Is.True); Assert.That(Save(stale,b),Is.False);
            Assert.That((bool)RuntimeAccess.Call(stale,"Consume",RuntimeAccess.Get<string>(a,"token")),Is.False);
            var oldReader=Store(); Assert.That(Save(first,b),Is.True);
            Assert.That((bool)RuntimeAccess.Call(oldReader,"Consume",RuntimeAccess.Get<string>(a,"token")),Is.False);
            Assert.That(RuntimeAccess.Get<string>(RuntimeAccess.Get<object>(Store(),"Snapshot"),"token"),Is.EqualTo(RuntimeAccess.Get<string>(b,"token")));
        }
    }
}
