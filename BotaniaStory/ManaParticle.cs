using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class ManaParticle
    {
        public Vec3d StartPos;
        public Vec3d EndPos;
        public float Progress;
        public float Speed;
        public float Size;
        public Vec4f Color; // ДОБАВИЛИ ЦВЕТ (R, G, B, Alpha)

        // Обновили конструктор, чтобы он принимал цвет
        public ManaParticle(Vec3d start, Vec3d end, float speed, float size, Vec4f color)
        {
            StartPos = start;
            EndPos = end;
            Speed = speed;
            Size = size;
            Color = color;
            Progress = 0f;
        }

        public Vec3d GetCurrentPosition()
        {
            double x = StartPos.X + (EndPos.X - StartPos.X) * Progress;
            double y = StartPos.Y + (EndPos.Y - StartPos.Y) * Progress;
            double z = StartPos.Z + (EndPos.Z - StartPos.Z) * Progress;
            return new Vec3d(x, y, z);
        }
    }
}