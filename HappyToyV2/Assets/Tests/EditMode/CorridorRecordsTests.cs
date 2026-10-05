using System;
using System.IO;
using NUnit.Framework;

namespace HappyToy.V2.Tests.EditMode
{
    public sealed class CorridorRecordsTests
    {
        string root, directory;
        Type type;
        [SetUp] public void Setup()
        {
            root = Path.GetFullPath("Temp/CorridorRecordTests");
            directory = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            type = Type.GetType("HappyToy.V2.CorridorRecords, Assembly-CSharp", true);
        }
        [TearDown] public void Cleanup()
        {
            var target = Path.GetFullPath(directory);
            Assert.That(target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }
        object Store(string path = null) => Activator.CreateInstance(type, path ?? directory, true);
        bool Record(object store, int seed, int count, bool won, float time) =>
            (bool)type.GetMethod("Record").Invoke(store, new object[] { seed, count, won, time });
        object Snapshot(object store) => type.GetProperty("Snapshot").GetValue(store);
        T Field<T>(object snapshot, string name) => (T)snapshot.GetType().GetField(name).GetValue(snapshot);
        string Primary => Path.Combine(directory, "corridor-records-v1.json");
        [Test] public void FinishedRunsKeepFastestEscapeAndHighestRecoveryAcrossRecreation()
        {
            var store = Store();
            Assert.That(Record(store, 1, 2, false, 60), Is.True);
            Assert.That(Record(store, 2, 5, true, 120), Is.True);
            Assert.That(Record(store, 3, 5, true, 90), Is.True);
            Assert.That(Record(store, 4, 4, false, 140), Is.True);
            var recreated = Store(); var data = Snapshot(recreated);
            Assert.That(Field<int>(data, "attempts"), Is.EqualTo(4)); Assert.That(Field<int>(data, "escapes"), Is.EqualTo(2));
            Assert.That(Field<int>(data, "bestRecovered"), Is.EqualTo(5)); Assert.That(Field<float>(data, "bestEscapeSeconds"), Is.EqualTo(90));
            Assert.That(Field<int>(data, "lastSeed"), Is.EqualTo(4)); Assert.That(Field<bool>(data, "lastEscaped"), Is.False);
            data.GetType().GetField("attempts").SetValue(data, 999);
            Assert.That(Field<int>(Snapshot(recreated), "attempts"), Is.EqualTo(4), "UI snapshot mutated stored progression");
            Assert.That(File.Exists(Primary + ".bak"), Is.True);
            Assert.That(Directory.GetFiles(directory, "*.tmp-*").Length, Is.Zero);
        }
        [Test] public void DamagedPrimaryRecoversBackupAndCanCommitTheNextResult()
        {
            var store = Store(); Record(store, 1, 3, false, 40); Record(store, 2, 5, true, 80);
            File.WriteAllText(Primary, "{broken");
            var recovered = Store(); Assert.That(Field<int>(Snapshot(recovered), "attempts"), Is.EqualTo(1));
            Assert.That(Record(recovered, 3, 4, false, 70), Is.True);
            Assert.That(Field<int>(Snapshot(Store()), "attempts"), Is.EqualTo(2));
            Assert.That(Field<int>(Snapshot(Store()), "bestRecovered"), Is.EqualTo(4));
        }
        [Test] public void FutureVersionIsPreservedAndInvalidResultsDoNotChangeStorage()
        {
            var store = Store();
            Assert.That(Record(store, 1, 3, true, 40), Is.False);
            Assert.That(Record(store, 1, 6, false, 40), Is.False);
            Assert.That(Record(store, 1, 4, false, float.NaN), Is.False);
            Assert.That(File.Exists(Primary), Is.False);
            const string future = "{\"version\":2,\"attempts\":7,\"futureData\":\"preserve\"}";
            File.WriteAllText(Primary, future);
            var futureStore = Store(); Assert.That(Record(futureStore, 4, 5, true, 50), Is.False);
            Assert.That(File.ReadAllText(Primary), Is.EqualTo(future));
        }
        [Test] public void StorageFailureKeepsInMemoryTotalsAndReturnsRecoverableStatus()
        {
            var blocked = Path.Combine(directory, "not-a-directory"); File.WriteAllText(blocked, "owned fixture");
            var store = Store(blocked); Assert.That(Record(store, 1, 4, false, 40), Is.False);
            Assert.That(Field<int>(Snapshot(store), "attempts"), Is.Zero);
            Assert.That((string)type.GetProperty("Status").GetValue(store), Does.Contain("기록하지 못했습니다"));
            Assert.That(File.ReadAllText(blocked), Is.EqualTo("owned fixture"));
        }
        [Test] public void IndependentInstancesMergeResultsAndAFileLeasePreventsLostUpdates()
        {
            var first = Store(); var second = Store();
            Assert.That(Record(first, 1, 3, false, 40), Is.True);
            Assert.That(Record(second, 2, 5, true, 80), Is.True);
            Assert.That(Field<int>(Snapshot(Store()), "attempts"), Is.EqualTo(2));
            using (var lease = new FileStream(Primary + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                Assert.That(Record(first, 3, 4, false, 60), Is.False);
            Assert.That(Record(first, 3, 4, false, 60), Is.True);
            Assert.That(Field<int>(Snapshot(Store()), "attempts"), Is.EqualTo(3));
            Assert.That(Field<int>(Snapshot(Store()), "escapes"), Is.EqualTo(1));
        }
        [Test] public void OversizedUnknownProfileIsPreservedRatherThanSilentlyReplaced()
        {
            string unknown = new string('x', 128 * 1024 + 1); File.WriteAllText(Primary, unknown);
            Assert.That(Record(Store(), 1, 5, true, 70), Is.False);
            Assert.That(File.ReadAllText(Primary), Is.EqualTo(unknown));
        }
    }
}
