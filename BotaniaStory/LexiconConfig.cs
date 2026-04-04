using System.Collections.Generic;

namespace botaniastory
{
    public class LexiconConfig
    {
        public float BookScale { get; set; } = 1.0f;
        public int Opacity { get; set; } = 100;
        public int Volume { get; set; } = 50;
        public bool DarkMode { get; set; } = false;
        public bool ShowPageNumbers { get; set; } = true;
        public bool EnableAnimations { get; set; } = true;
        public string OwnerName { get; set; } = "Игрок";
        public string CustomTitle { get; set; } = "Гайд";

        // НОВОЕ: Пасхалка для игроков! Здесь будет храниться их кастомный UI
        public Dictionary<string, double[]> CustomUI { get; set; } = null;
    }
}