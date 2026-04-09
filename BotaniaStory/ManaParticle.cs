using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class ManaParticle
    {
        public Vec3d StartPos;
        public Vec3d EndPos;
        public float Progress; // От 0.0f (старт) до 1.0f (финиш)
        public float Speed;    // Как быстро заполняется Progress (например, 2.0f в секунду)
        public float Size;

        public ManaParticle(Vec3d start, Vec3d end, float speed, float size)
        {
            StartPos = start;
            EndPos = end;
            Speed = speed;
            Size = size;
            Progress = 0f;
        }

        // Вычисляет текущую позицию в 3D пространстве на основе Progress
        public Vec3d GetCurrentPosition()
        {
            // Lerp (Linear Interpolation) - находит точку на отрезке между Start и End
            double x = StartPos.X + (EndPos.X - StartPos.X) * Progress;
            double y = StartPos.Y + (EndPos.Y - StartPos.Y) * Progress;
            double z = StartPos.Z + (EndPos.Z - StartPos.Z) * Progress;
            return new Vec3d(x, y, z);
        }
    }
}