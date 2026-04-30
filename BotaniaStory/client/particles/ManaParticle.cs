using System;
using Vintagestory.API.MathTools;

namespace BotaniaStory.client.particles
{
    public class ManaParticle
    {
        public Vec3d StartPos;
        public Vec3d EndPos;
        public float Progress;
        public float Speed;
        public float Size;
        public Vec4f Color;
        public Vec3d Offset; // <-- ДОБАВЛЕНО смещение для распыления

        public ManaParticle(Vec3d start, Vec3d end, float speed, float size, Vec4f color, Vec3d offset)
        {
            StartPos = start;
            EndPos = end;
            Speed = speed;
            Size = size;
            Color = color;
            Offset = offset;
            Progress = 0f;
        }

        public Vec3d GetCurrentPosition()
        {
            // Базовое линейное движение
            double x = StartPos.X + (EndPos.X - StartPos.X) * Progress;
            double y = StartPos.Y + (EndPos.Y - StartPos.Y) * Progress;
            double z = StartPos.Z + (EndPos.Z - StartPos.Z) * Progress;

            // Плавно применяем смещение. 
            // Math.PI * Progress даст дугу: от 0 -> 1 -> 0 по мере движения.
            double wave = Math.Sin(Progress * Math.PI);

            x += Offset.X * wave;
            y += Offset.Y * wave;
            z += Offset.Z * wave;

            return new Vec3d(x, y, z);
        }
    }
}