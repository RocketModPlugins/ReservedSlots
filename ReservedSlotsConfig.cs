using Rocket.Core.Configuration;

namespace ReservedSlots
{
    public class ReservedSlotsConfig
    {
        public bool ReservedSlotEnable { get; set; } = true;

        [ConfigArray]
        public string[] Groups { get; set; } =  {
            "moderator",
            "admin"
        };

        public byte ReservedSlotCount { get; set; } = 2;
        public bool AllowFill { get; set; } = false;
        public bool AllowDynamicMaxSlot { get; set; } = false;
        public byte MaxSlotCount { get; set; } = 24;
        public byte MinSlotCount { get; set; } = 16;
    }
}