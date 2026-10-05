using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class ChapterRecordsTests
    {
        string directory,path;
        Type storeType,reportType;
        [SetUp] public void Setup()
        {
            directory=Path.GetFullPath(Path.Combine("Temp","SchoolRecordTests",Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory); path=Path.Combine(directory,"school-records-v1.json");
            storeType=RequireType("ChapterRecords"); reportType=RequireType("ChapterRunReport");
        }
        [TearDown] public void Cleanup()
        {
            var allowed=Path.GetFullPath("Temp/SchoolRecordTests")+Path.DirectorySeparatorChar;
            Assert.That(directory.StartsWith(allowed,StringComparison.OrdinalIgnoreCase),Is.True);
            if(Directory.Exists(directory)) Directory.Delete(directory,true);
        }
        object Store(bool write=true,string target=null) => Activator.CreateInstance(storeType,target??directory,write);
        object Report(int recovered=3,bool escaped=false,float seconds=75)
        {
            var report=Activator.CreateInstance(reportType); Set(report,"token",Guid.NewGuid().ToString("N"));
            Set(report,"endedUtc",DateTimeOffset.UtcNow.ToString("O",CultureInfo.InvariantCulture)); Set(report,"defeatSource",escaped?"":"Weeping mannequin");
            Set(report,"recovered",recovered); Set(report,"escaped",escaped); Set(report,"seconds",seconds);
            Set(report,"firecrackersRemaining",2); Set(report,"staminaRemaining",.7f); Set(report,"actionsComplete",true);
            Set(report,"throws",1); Set(report,"hides",2); Set(report,"closedDoors",3); Set(report,"distance",150f); return report;
        }
        bool Record(object store,object report) => (bool)Call(store,"Record",report);
        object Summary(object store) => Get<object>(store,"Snapshot");
        [Test] public void SchoolResultsKeepBestEscapeRecentActionsAndBoundedIndependentHistory()
        {
            var store=Store(); Assert.That(Record(store,Report()),Is.True);
            Assert.That(Record(store,Report(5,true,120)),Is.True); Assert.That(Record(store,Report(5,true,90)),Is.True);
            object last=null;
            for(int i=0;i<67;i++) { last=Report(4,false,80+i); Assert.That(Record(store,last),Is.True); }
            var summary=Summary(Store()); Assert.That(Get<int>(summary,"attempts"),Is.EqualTo(70));
            Assert.That(Get<int>(summary,"escapes"),Is.EqualTo(2)); Assert.That(Get<int>(summary,"bestRecovered"),Is.EqualTo(5));
            Assert.That(Get<float>(summary,"bestEscapeSeconds"),Is.EqualTo(90));
            Assert.That(Get<Array>(summary,"recent").Length,Is.EqualTo(8)); Assert.That(Get<Array>(summary,"receipts").Length,Is.EqualTo(64));
            var first=Get<Array>(summary,"recent").GetValue(0); Assert.That(Get<string>(first,"token"),Is.EqualTo(Get<string>(last,"token")));
            Assert.That(Get<int>(first,"closedDoors"),Is.EqualTo(3)); Set(first,"firecrackersRemaining",0);
            Assert.That(Get<int>(Get<Array>(Summary(store),"recent").GetValue(0),"firecrackersRemaining"),Is.EqualTo(2));
            Assert.That(new FileInfo(path).Length,Is.LessThan(128*1024));
            Assert.That(File.Exists(Path.Combine(directory,"corridor-records-v1.json")),Is.False);
        }
        [Test] public void StaleWritersMergeUnderLeaseAndDuplicateRecentRunTokensCountOnce()
        {
            var first=Store(); var second=Store(); var a=Report(); var b=Report(5,true,80);
            Assert.That(Record(first,a),Is.True); Assert.That(Record(second,b),Is.True); Assert.That(Record(first,a),Is.True);
            Assert.That(Get<int>(Summary(Store()),"attempts"),Is.EqualTo(2));
            using(var lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                Assert.That(Record(first,Report()),Is.False);
            Assert.That(Record(first,Report()),Is.True); Assert.That(Get<int>(Summary(Store()),"attempts"),Is.EqualTo(3));
            Assert.That(Directory.GetFiles(directory,"*.tmp-*").Length,Is.Zero);
        }
        [Test] public void CorruptUnknownAndOversizedPrimaryFilesArePreservedEvenWithReadableBackup()
        {
            var store=Store(); Assert.That(Record(store,Report()),Is.True); var good=File.ReadAllText(path); File.WriteAllText(path+".bak",good);
            foreach(var bytes in new[]{"{\"version\":999}","{}","not json",new string('x',128*1024+1)})
            {
                File.WriteAllText(path,bytes); var protectedStore=Store(); Assert.That(Get<bool>(protectedStore,"Writable"),Is.False);
                Assert.That(Get<int>(Summary(protectedStore),"attempts"),Is.EqualTo(1)); Assert.That(Record(protectedStore,Report()),Is.False);
                Assert.That(File.ReadAllText(path),Is.EqualTo(bytes)); Assert.That(File.ReadAllText(path+".bak"),Is.EqualTo(good));
            }
        }
        [Test] public void ValidPrimaryStaysWritableAndArchivesDamagedBackupBytesWithoutReplacingThem()
        {
            var store=Store(); Assert.That(Record(store,Report()),Is.True);
            foreach(var bytes in new[]{"not valid backup JSON","{\"version\":999}",new string('x',128*1024+1)})
            {
                string primary=File.ReadAllText(path); int attempts=Get<int>(Summary(Store()),"attempts");
                File.WriteAllText(path+".bak",bytes); var fresh=Store(); Assert.That(Get<bool>(fresh,"Writable"),Is.True);
                var oldArchives=Directory.GetFiles(directory,"school-records-v1.json.bak-preserved-*");
                Assert.That(Record(fresh,Report()),Is.True); Assert.That(Get<int>(Summary(Store()),"attempts"),Is.EqualTo(attempts+1));
                var archives=Array.FindAll(Directory.GetFiles(directory,"school-records-v1.json.bak-preserved-*"),x=>Array.IndexOf(oldArchives,x)<0);
                Assert.That(archives.Length,Is.EqualTo(1)); Assert.That(File.ReadAllText(archives[0]),Is.EqualTo(bytes));
                Assert.That(File.ReadAllText(path+".bak"),Is.EqualTo(primary));
            }
        }
        [Test] public void LockedDamagedBackupKeepsPrimaryAndOriginalBytesAndAllowsSafeRetry()
        {
            var store=Store(); Assert.That(Record(store,Report()),Is.True); string primary=File.ReadAllText(path);
            const string bytes="owned locked damaged backup"; File.WriteAllText(path+".bak",bytes); var fresh=Store();
            using(var lease=new FileStream(path+".bak",FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            {
                Assert.That(Get<bool>(fresh,"Writable"),Is.True); Assert.That(Record(fresh,Report()),Is.False);
                Assert.That(File.ReadAllText(path),Is.EqualTo(primary)); Assert.That(Directory.GetFiles(directory,"*.bak-preserved-*").Length,Is.Zero);
            }
            Assert.That(File.ReadAllText(path+".bak"),Is.EqualTo(bytes)); var retry=Report(); Assert.That(Record(fresh,retry),Is.True);
            Assert.That(Record(fresh,retry),Is.True); Assert.That(Get<int>(Summary(Store()),"attempts"),Is.EqualTo(2));
            Assert.That(Directory.GetFiles(directory,"*.tmp-*").Length,Is.Zero);
        }
        [Test] public void MissingPrimaryRecoversValidBackupAndWriteFailureCanRetryWithoutDataLoss()
        {
            var store=Store(); Assert.That(Record(store,Report()),Is.True); Assert.That(Record(store,Report(5,true,90)),Is.True);
            File.Delete(path); var recovered=Store(); Assert.That(Get<int>(Summary(recovered),"attempts"),Is.EqualTo(1));
            Assert.That(Record(recovered,Report(5,true,80)),Is.True); Assert.That(Get<int>(Summary(Store()),"attempts"),Is.EqualTo(2));
            string blocked=Path.Combine(directory,"blocked"); File.WriteAllText(blocked,"owned school record write blocker");
            var failure=Store(true,blocked); Assert.That(Record(failure,Report()),Is.False);
            Assert.That(File.ReadAllText(blocked),Is.EqualTo("owned school record write blocker")); File.Delete(blocked);
            Assert.That(Record(failure,Report()),Is.True); Assert.That(Get<int>(Summary(failure),"attempts"),Is.EqualTo(1));
        }
        [Test] public void ReadOnlyInvalidResourcesAndImpossibleEscapesNeverWrite()
        {
            Assert.That(Record(Store(false),Report()),Is.False); Assert.That(Record(Store(),Report(4,true)),Is.False);
            var invalid=Report(); Set(invalid,"staminaRemaining",float.NaN); Assert.That(Record(Store(),invalid),Is.False);
            invalid=Report(); Set(invalid,"firecrackersRemaining",6); Assert.That(Record(Store(),invalid),Is.False);
            invalid=Report(); Set(invalid,"throws",-1); Assert.That(Record(Store(),invalid),Is.False);
            Assert.That(Record(Store(),null),Is.False); Assert.That(File.Exists(path),Is.False);
        }
    }
}
