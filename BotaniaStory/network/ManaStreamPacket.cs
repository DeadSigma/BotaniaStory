using ProtoBuf;

namespace BotaniaStory.network
{
    // Атрибут ProtoContract обязателен, чтобы Vintage Story смогла переслать это по сети
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ManaStreamPacket
    {
        public double StartX;
        public double StartY;
        public double StartZ;

        public double EndX;
        public double EndY;
        public double EndZ;
    }
}