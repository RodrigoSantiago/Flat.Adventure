using System.IO;

namespace Game.Worlds.Storage {
    public interface IStreamTransfer {
        public Stream CreateInput(string file);
        public Stream CreateOutput(string file, bool temp);
        public void Flush(Stream stream);
        public void UpgradeTempOutput(string file);
    }
}