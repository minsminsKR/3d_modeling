using System;
using System.IO;
using UnityEngine;

namespace HappyToy.V2
{
    // Finished-run progression. The current maze is not a resume checkpoint.
    public sealed class CorridorRecords
    {
        const int MaximumAttempts = 1000000;
        const float MaximumSeconds = 1000000000;
        [Serializable]
        public sealed class Summary
        {
            public int version, attempts, escapes, bestRecovered, lastSeed;
            public float bestEscapeSeconds, lastSeconds;
            public bool lastEscaped;
        }
        readonly string path;
        readonly bool allowWrite;
        Summary state;
        bool futureVersion;
        bool protectedProfile;
        public bool PersistenceEnabled => allowWrite;
        public bool Writable => allowWrite && !futureVersion && !protectedProfile && state.attempts < MaximumAttempts;
        public string Status { get; private set; } = "";
        public Summary Snapshot => new Summary { version = state.version, attempts = state.attempts, escapes = state.escapes,
            bestRecovered = state.bestRecovered, lastSeed = state.lastSeed, bestEscapeSeconds = state.bestEscapeSeconds,
            lastSeconds = state.lastSeconds, lastEscaped = state.lastEscaped };

        public CorridorRecords(string directory, bool allowWrite)
        {
            path = Path.Combine(Path.GetFullPath(directory), "corridor-records-v1.json"); this.allowWrite = allowWrite;
            var primary = Read(path); var backup = Read(path + ".bak"); state = primary ?? backup ?? new Summary { version = 1 };
            if (futureVersion) Status = "더 새로운 버전의 탐색 기록을 보호하기 위해 기록을 변경하지 않습니다.";
            else if (protectedProfile) Status = "기존 탐색 기록을 안전하게 읽을 수 없어 변경하지 않습니다.";
            else if (state.attempts >= MaximumAttempts) Status = "탐색 기록의 저장 한도에 도달했습니다.";
            else if (primary == null && backup != null) Status = "이전 탐색 기록을 복구했습니다.";
        }
        Summary Read(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                if (new FileInfo(file).Length > 128 * 1024) { protectedProfile = true; return null; }
                var data = JsonUtility.FromJson<Summary>(File.ReadAllText(file));
                if (data != null && data.version > 1) { futureVersion = true; return null; }
                return Valid(data) ? data : null;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { protectedProfile = true; return null; }
            catch (ArgumentException)
            { return null; }
        }
        static bool Valid(Summary data) => data != null && data.version == 1 && data.attempts > 0 && data.attempts <= MaximumAttempts &&
            data.escapes >= 0 && data.escapes <= data.attempts && data.bestRecovered >= 0 && data.bestRecovered <= 5 &&
            StealthRules.Finite(data.lastSeconds) && data.lastSeconds >= 0 && data.lastSeconds <= MaximumSeconds &&
            StealthRules.Finite(data.bestEscapeSeconds) && data.bestEscapeSeconds >= 0 && data.bestEscapeSeconds <= MaximumSeconds &&
            (!data.lastEscaped || data.escapes > 0 && data.bestEscapeSeconds <= data.lastSeconds) &&
            (data.escapes == 0 ? data.bestEscapeSeconds == 0 : data.bestRecovered == 5);

        public bool Record(int seed, int recovered, bool escaped, float seconds)
        {
            if (!Writable) return false;
            if (recovered < 0 || recovered > 5 || escaped && recovered != 5 ||
                !StealthRules.Finite(seconds) || seconds < 0 || seconds > MaximumSeconds)
            { Status = "탐색 결과가 올바르지 않아 기록하지 않았습니다."; return false; }
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    // Another game instance may have finished since this one opened.
                    // Merge under a shared file lease instead of overwriting its totals.
                    var fresh = new CorridorRecords(Path.GetDirectoryName(path), true);
                    if (!fresh.Writable)
                    { state = fresh.state; futureVersion = fresh.futureVersion; protectedProfile = fresh.protectedProfile; Status = fresh.Status; return false; }
                    var next = fresh.Snapshot;
                    next.attempts++; next.lastSeed = seed; next.lastEscaped = escaped; next.lastSeconds = seconds;
                    next.bestRecovered = Math.Max(next.bestRecovered, recovered);
                    if (escaped)
                    {
                        if (next.escapes == 0 || seconds < next.bestEscapeSeconds) next.bestEscapeSeconds = seconds;
                        next.escapes++;
                    }
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream))
                    { writer.Write(JsonUtility.ToJson(next, true)); writer.Flush(); stream.Flush(true); }
                    if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
                    state = next; Status = "이번 탐색 결과를 기록했습니다."; return true;
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is NotSupportedException)
            { Status = "탐색 결과를 기기에 기록하지 못했습니다.\n다시 저장하거나 이 화면에서 결과를 확인하세요."; return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
            }
        }
    }
}
