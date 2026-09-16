using BotaniaStory.entities;
using Vintagestory.API.Common;

namespace BotaniaStory.systems
{
    public class ElementiumPixieSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterEntity(
                "ElementiumPixie",
                typeof(EntityElementiumPixie)
            );

            api.RegisterEntityBehaviorClass(
                "elementiumpixies",
                typeof(EntityBehaviorElementiumPixies)
            );
        }
    }
}
