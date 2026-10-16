using System;
using System.IO;
using NUnit.Framework;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class CheckpointTrapCodecTests
    {
        string directory;
        [SetUp] public void Setup()
        {
            directory=Path.GetFullPath(Path.Combine("Temp","CheckpointTrapCodecTests",Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
        }
        [TearDown] public void Cleanup()
        {
            string allowed=Path.GetFullPath(Path.Combine("Temp","CheckpointTrapCodecTests"))+Path.DirectorySeparatorChar;
            Assert.That(directory.StartsWith(allowed,StringComparison.OrdinalIgnoreCase),Is.True);
            if(Directory.Exists(directory)) Directory.Delete(directory,true);
        }
        static string Legacy(string kind)
        {
            if(kind=="Corridor") return File.ReadAllText("Assets/Tests/Fixtures/corridor-checkpoint-state.json");
            return "{\"version\":1,\"simulationVersion\":1,\"token\":\""+Guid.NewGuid().ToString("N")+"\",\"scene\":\"Assets/Annex/SchoolAnnex.unity\",\"recovered\":0,\"seconds\":17,\"player\":{\"position\":{\"x\":-7.8,\"y\":0.02},\"stamina\":0.7,\"stock\":2},\"cyclopse\":{\"door\":\"\"},\"portraitActor\":{\"door\":\"\"},\"nurseryActor\":{\"door\":\"\"},\"mannequin\":{\"rotation\":{\"w\":1},\"visualRotation\":{\"w\":1}},\"mask\":{\"rotation\":{\"w\":1}},\"doors\":[],\"supplies\":[]}";
        }
        static string AddRoot(string json,string fields) => json.Insert(json.LastIndexOf('}'),","+fields);
        static object Decode(string kind,string json) => Call(RequireType(kind+"Checkpoint"),"FromJson",json);
        object Store(string kind) => Activator.CreateInstance(RequireType(kind+"CheckpointStore"),directory,true);
        string Slot(string kind) => Path.Combine(directory,kind=="Corridor"?"corridor-suspend-v1.json":"chapter-suspend-v1.json");

        [TestCase("Corridor"),TestCase("Chapter")]
        public void AbsentAndNullLegacyTrapFieldsRemainAbsentAcrossCopyAndStorage(string kind)
        {
            string legacy=Legacy(kind);
            foreach(string json in new[]{legacy,AddRoot(legacy,"\"traps\":null"),AddRoot(legacy,"\"oldMetadata\":{\"traps\":{}}")})
            {
                var data=Decode(kind,json); Assert.That(Get<object>(data,"traps"),Is.Null);
                Call(data,"Validate"); Assert.That(Get<object>(Call(data,"Copy"),"traps"),Is.Null);
                Assert.That((string)Call(data,"ToJson"),Does.Not.Contain("\"traps\""));
                // Read the user's historical bytes through the actual slot
                // constructor, then rewrite/reopen them without inventing stock.
                File.WriteAllText(Slot(kind),json); var store=Store(kind);
                Assert.That(Get<bool>(store,"HasRun"),Is.True); Assert.That(Get<object>(Get<object>(store,"Snapshot"),"traps"),Is.Null);
                Assert.That((bool)Call(store,"Save",data),Is.True);
                Assert.That(File.ReadAllText(Slot(kind)),Does.Not.Contain("\"traps\""));
                Assert.That(Get<object>(Get<object>(Store(kind),"Snapshot"),"traps"),Is.Null);
            }
        }

        [TestCase("Corridor"),TestCase("Chapter")]
        public void ExplicitMalformedTrapFieldsAreRejectedAndTheirRawSlotBytesArePreserved(string kind)
        {
            string legacy=Legacy(kind);
            foreach(string fields in new[]{"\"traps\":{}","\"traps\":{\"stock\":1}",
                "\"traps\":{\"stock\":-1,\"supplies\":[],\"armed\":[]}",
                "\"traps\":null,\"traps\":{\"stock\":0,\"supplies\":[],\"armed\":[]}"})
            {
                string corrupt=AddRoot(legacy,fields);
                Assert.Throws<ArgumentException>(()=>Decode(kind,corrupt));
                File.WriteAllText(Slot(kind),corrupt); var store=Store(kind);
                Assert.That(Get<bool>(store,"Writable"),Is.False); Assert.That(Get<bool>(store,"HasRun"),Is.False);
                Assert.That((bool)Call(store,"Save",Decode(kind,legacy)),Is.False);
                Assert.That(File.ReadAllText(Slot(kind)),Is.EqualTo(corrupt));
            }
        }

        [TestCase("Corridor"),TestCase("Chapter")]
        public void ExplicitZeroStockTrapStateSurvivesCopyAndStorageWithoutBecomingLegacy(string kind)
        {
            var data=Decode(kind,AddRoot(Legacy(kind),"\"traps\":{\"stock\":0,\"cooldown\":0,\"supplies\":[],\"armed\":[]}"));
            var trap=Get<object>(data,"traps"); Assert.That(trap,Is.Not.Null); Assert.That(Get<int>(trap,"stock"),Is.Zero);
            Assert.That((string)Call(data,"ToJson"),Does.Contain("\"traps\""));
            Assert.That(Get<int>(Get<object>(Call(data,"Copy"),"traps"),"stock"),Is.Zero);
            var store=Store(kind); Assert.That((bool)Call(store,"Save",data),Is.True);
            Set(trap,"stock",2); // Snapshots must own the nested trap data too.
            Assert.That(Get<int>(Get<object>(Get<object>(store,"Snapshot"),"traps"),"stock"),Is.Zero);
            var reopened=Store(kind); Assert.That(Get<bool>(reopened,"HasRun"),Is.True);
            var snapshot=Get<object>(reopened,"Snapshot"); var restored=Get<object>(snapshot,"traps");
            Assert.That(restored,Is.Not.Null); Assert.That(Get<int>(restored,"stock"),Is.Zero);
            Assert.That(Get<Array>(restored,"supplies"),Is.Empty); Assert.That(Get<Array>(restored,"armed"),Is.Empty);
            Call(snapshot,"Validate");
        }
    }
}
