using System;
using System.IO;
using System.Text;
using System.Text.Json;
using HappyToy.V2;

// The real file store is exercised with a minimal serializer stand-in. Unity's
// actual checkpoint validation and scene restoration remain integration tests.
namespace HappyToy.V2
{
    public sealed class TemplePrologueCheckpoint
    {
        public string token { get; set; }
        public int phase { get; set; }
        public void Validate()
        {
            if (!Guid.TryParseExact(token, "N", out _) || phase < 0 || phase > 5)
                throw new ArgumentException("Invalid test checkpoint");
        }
        public string ToJson() { Validate(); return JsonSerializer.Serialize(this); }
        public static TemplePrologueCheckpoint FromJson(string text)
        {
            try
            {
                var data = JsonSerializer.Deserialize<TemplePrologueCheckpoint>(text);
                if (data == null) throw new ArgumentException("Empty checkpoint");
                data.Validate(); return data;
            }
            catch (JsonException error) { throw new ArgumentException("Bad checkpoint", error); }
        }
        public TemplePrologueCheckpoint Copy() => FromJson(ToJson());
    }
}

static class TempleCheckpointStoreTestProgram
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static TemplePrologueCheckpoint NewCheckpoint(int phase) => new TemplePrologueCheckpoint { token = Guid.NewGuid().ToString("N"), phase = phase };
    static void Main()
    {
        string directory = Path.Combine(Path.GetTempPath(), "temple-store-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "temple-suspend-v1.json");
            var initial = new TemplePrologueCheckpointStore(directory, true);
            var original = NewCheckpoint(1);
            Require(initial.Save(original), "Initial save failed");
            var staleSave = new TemplePrologueCheckpointStore(directory, true);
            var staleConsume = new TemplePrologueCheckpointStore(directory, true);
            var edited = original.Copy(); edited.phase = 4;
            string external = edited.ToJson(); File.WriteAllText(path, external);
            Require(!staleSave.Save(NewCheckpoint(2)), "A valid same-token external edit was overwritten");
            Require(File.ReadAllText(path) == external, "Save did not preserve the external edit");
            Require(!staleConsume.Consume(original.token) && staleConsume.Conflict, "A changed same-token record was consumed");
            Require(File.ReadAllText(path) == external && !File.Exists(path + ".used"), "Consume moved the changed record");
            var fresh = new TemplePrologueCheckpointStore(directory, true);
            Require(fresh.Snapshot.phase == 4 && fresh.Consume(original.token), "Fresh valid record could not be consumed");
            Require(!File.Exists(path) && File.ReadAllText(path + ".used") == external, "Normal consume lost its source");
            var empty = new TemplePrologueCheckpointStore(directory, true);
            var next = NewCheckpoint(3);
            Require(empty.Save(next), "A fresh save after consumption failed");
            var staleFormatting = new TemplePrologueCheckpointStore(directory, true);
            var withBom = new UTF8Encoding(true);
            File.WriteAllText(path, next.ToJson(), withBom);
            byte[] bomBytes = File.ReadAllBytes(path);
            Require(!staleFormatting.Consume(next.token) && staleFormatting.Conflict,
                "A byte-level encoding change was not protected");
            Require(Convert.ToBase64String(File.ReadAllBytes(path)) == Convert.ToBase64String(bomBytes),
                "A byte-level encoding change was lost");
            var freshBom = new TemplePrologueCheckpointStore(directory, true);
            Require(freshBom.HasRun && freshBom.Consume(next.token), "A fresh BOM record could not be consumed");
            Console.WriteLine("TEMPLE_CHECKPOINT_STORE_PASS");
        }
        finally { Directory.Delete(directory, true); }
    }
}
