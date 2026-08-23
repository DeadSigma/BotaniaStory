using System;
using Vintagestory.API.MathTools;

namespace BotaniaStory.client.particles
{
    public class PylonParticle
    {
        public Vec3d Position = new Vec3d();
        public Vec3d Velocity = new Vec3d();
        public Vec4f Color = new Vec4f(1f, 1f, 1f, 1f);

        public float Size = 0.1f;
        public float Life;
        public float MaxLife = 1f;
        public int TextureIndex;
        public bool ShrinkOnDeath;

        // Всё ниже по умолчанию выключено.
        public float Age;
        public float Drag;                              // 0 = без торможения
        public float Gravity;                           // 0 = без гравитации
        public Vec3d SwirlAxis = new Vec3d(0, 1, 0);    
        public float SwirlStrength;                     // 0 = без закрутки; рад/сек, знак = сторона
        public float WobbleFreq = 6f;
        public float WobblePhase;

        public float FadeIn = 0.05f;                    // доля жизни на появление
        public float FadeStart = 0.7f;                  // с какой доли ПУТИ начинать гаснуть

        public float LifeRatio => MaxLife <= 0f ? 0f : Life / MaxLife;
        public float Progress => 1f - LifeRatio;        // 0 при рождении - 1 при смерти

        public float Fade
        {
            get
            {
                float t = Progress;
                float a = 1f;
                if (FadeIn > 0f && t < FadeIn) a = t / FadeIn;
                if (t > FadeStart)
                {
                    float k = (t - FadeStart) / Math.Max(0.0001f, 1f - FadeStart);
                    a *= 1f - GameMath.Clamp(k, 0f, 1f);
                }
                return a;
            }
        }
    }
}