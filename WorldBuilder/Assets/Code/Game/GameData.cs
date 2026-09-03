using System.Collections.Generic;

namespace Game {
    public class GameData {
        public static GameData Instance { get; } = new ();
        
        public Dictionary<long, PlayerData> players = new();
    }
}