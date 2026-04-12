using System.Collections.Generic;

namespace botaniastory
{
    public class LexiconConfig
    {
        public float BookScale { get; set; } = 1.0f;
        public int Volume { get; set; } = 50;
        public int FlowerVolume = 50;
        public int SpreaderVolume = 50;
        public bool MouseWheelPaging = false; 
        public bool RightClickBack = false;
        public int PoolVolume { get; set; } = 50;
        public int WandVolume { get; set; } = 50;


        // НОВОЕ: для игроков! Здесь будет храниться их кастомный UI
        public Dictionary<string, double[]> CustomUI { get; set; } = null;
    }
}