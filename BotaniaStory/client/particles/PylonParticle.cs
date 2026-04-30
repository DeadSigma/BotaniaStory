using Vintagestory.API.MathTools;

namespace BotaniaStory.client.particles
{
    public class PylonParticle
    {
        public Vec3d Position;
        public Vec3d Velocity;
        public float Size;
        public float Life;
        public float MaxLife;
        public Vec4f Color;
        public int TextureIndex; // 0-3 для искр, 4 для портала
        public bool ShrinkOnDeath = true;

        // Для красивого затухания и уменьшения
        public float LifeRatio => Life / MaxLife;
    }
}