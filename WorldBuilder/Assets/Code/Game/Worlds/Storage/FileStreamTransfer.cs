using System.IO;

namespace Game.Worlds.Storage {
    public class FileStreamTransfer : IStreamTransfer {

        public Stream CreateInput(string file) {
            return new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
        }

        public Stream CreateOutput(string file, bool temp) {
            return new FileStream(
                file + (temp ? ".tmp" : ""),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 64 * 1024,
                options: FileOptions.WriteThrough);
        }

        public void Flush(Stream stream) {
            if (stream is FileStream fileStream) {
                fileStream.Flush(true);
            } else {
                stream.Flush();
            }
        }

        public void UpgradeTempOutput(string file) {
            File.Replace(file + ".tmp", file, file + ".tmp.bkp");
        }
    }
}