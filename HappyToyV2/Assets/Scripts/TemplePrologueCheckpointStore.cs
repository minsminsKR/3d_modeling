using System;
using System.IO;
using System.Text;

namespace HappyToy.V2
{
    // Separate from both existing chapter slots. Corrupt or externally changed
    // files remain untouched; a consumed snapshot is moved out of the live slot.
    public sealed class TemplePrologueCheckpointStore
    {
        readonly string path;
        readonly bool allowWrite;
        TemplePrologueCheckpoint state;
        byte[] observedBytes;
        bool protectedFile;
        public bool HasRun => state != null;
        public bool Writable => allowWrite && !protectedFile;
        public bool Conflict { get; private set; }
        public string Status { get; private set; } = "";
        public TemplePrologueCheckpoint Snapshot => state?.Copy();
        public TemplePrologueCheckpointStore(string directory, bool write)
        {
            path = Path.Combine(Path.GetFullPath(directory), "temple-suspend-v1.json"); allowWrite = write;
            try
            {
                if (!File.Exists(path)) return;
                if (new FileInfo(path).Length > 128 * 1024) throw new ArgumentException("Oversized checkpoint");
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length > 128 * 1024) throw new ArgumentException("Oversized checkpoint");
                using (var reader = new StreamReader(new MemoryStream(bytes)))
                    state = TemplePrologueCheckpoint.FromJson(reader.ReadToEnd());
                observedBytes = bytes;
            }
            catch (Exception error) when (error is ArgumentException || error is IOException || error is UnauthorizedAccessException)
            { protectedFile = true; Status = "사찰 중단 기록을 읽을 수 없어 원문을 보존했습니다."; }
        }
        bool Same(TemplePrologueCheckpointStore other)
        {
            if (other.protectedFile || other.state?.token != state?.token) return false;
            if (observedBytes == null || other.observedBytes == null) return observedBytes == other.observedBytes;
            if (observedBytes.Length != other.observedBytes.Length) return false;
            for (int i = 0; i < observedBytes.Length; i++)
                if (observedBytes[i] != other.observedBytes[i]) return false;
            return true;
        }
        public bool Save(TemplePrologueCheckpoint data)
        {
            if (!Writable || data == null) return false;
            try { data.Validate(); } catch (ArgumentException) { Status = "현재 사찰 상태를 안전하게 저장할 수 없습니다."; return false; }
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                var copy = data.Copy();
                var bytes = Encoding.UTF8.GetBytes(data.ToJson());
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    if (!Same(new TemplePrologueCheckpointStore(Path.GetDirectoryName(path), true)))
                    { Status = "다른 실행에서 사찰 중단 기록이 변경되어 저장하지 않았습니다."; return false; }
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                    if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
                    state = copy; observedBytes = bytes; Status = "사찰 탐색을 중단 저장했습니다."; return true;
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is NotSupportedException)
            { Status = "사찰 기록을 저장하지 못했습니다. 현재 탐색은 유지됩니다."; return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
            }
        }
        public bool Consume(string token)
        {
            Conflict = false;
            if (!Writable || state == null || state.token != token) return false;
            try
            {
                using (var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    if (!Same(new TemplePrologueCheckpointStore(Path.GetDirectoryName(path), true)))
                    { Conflict = true; Status = "다른 실행에서 사찰 중단 기록이 변경되었습니다."; return false; }
                    if (File.Exists(path + ".used")) File.Replace(path, path + ".used", null); else File.Move(path, path + ".used");
                    state = null; observedBytes = null; Status = "사찰 탐색을 이어받았습니다."; return true;
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is NotSupportedException)
            { Status = "사찰 기록을 이어받지 못했습니다. 파일을 보존했습니다."; return false; }
        }
    }
}
