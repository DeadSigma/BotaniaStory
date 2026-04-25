using System.Collections.Generic;

namespace botaniastory
{
    public class LexiconConfig
    {
        public float BookScale { get; set; } = 1.0f;
        public int Volume { get; set; } = 50;
        public int FlowerVolume = 50;
        public int SpreaderVolume = 50;
        public bool MouseWheelPaging = true; 
        public bool RightClickBack = true;
        public int PoolVolume { get; set; } = 50;
        public int AltarVolume { get; set; } = 50;
        public int WandVolume { get; set; } = 50;
        public int ApothecaryVolume { get; set; } = 50;
        public int PlateVolume { get; set; } = 50;
        public int PortalVolume { get; set; } = 50;
        public Dictionary<string, double[]> CustomUI { get; set; } = null;
    }
}