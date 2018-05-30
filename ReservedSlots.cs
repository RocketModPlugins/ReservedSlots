using System.Linq;
using Rocket.Core.Logging;
using Rocket.Core.Plugins;
using SDG.Unturned;
using Rocket.API.DependencyInjection;
using Rocket.API.Eventing;
using Rocket.API.Permissions;
using Rocket.API.Player;
using Rocket.Core.Player.Events;
using Rocket.Unturned.Player;
using Rocket.Unturned.Player.Events;

namespace ReservedSlots
{
    public class ReservedSlots : Plugin<ReservedSlotsConfig>,
        IEventListener<UnturnedPlayerPreConnectEvent>,
        IEventListener<PlayerConnectedEvent>,
        IEventListener<PlayerDisconnectedEvent>
    {
        public ReservedSlots(IDependencyContainer container) : base("ReservedSlots", container)
        {
        }

        private byte _lastMaxSlotCount;

        protected override void OnLoad(bool isFromReload)
        {
            Logger.Log(
                $"Reserved Slots enabled: {ConfigurationInstance.ReservedSlotEnable}, Count: {ConfigurationInstance.ReservedSlotCount}, Allowfill: {ConfigurationInstance.AllowFill}, DynamicSlots: {ConfigurationInstance.AllowDynamicMaxSlot}, min: {ConfigurationInstance.MinSlotCount}, max:{ConfigurationInstance.MaxSlotCount}.");
            if (ConfigurationInstance.AllowDynamicMaxSlot)
            {
                if (ConfigurationInstance.MinSlotCount < 2)
                {
                    Logger.LogError("Reserved Slots Config Error: Minimum slots is set to 0, changing to the Unturned server default of 8 slots.");
                    ConfigurationInstance.MinSlotCount = 8;
                }
                if (ConfigurationInstance.MaxSlotCount > 48)
                {
                    Logger.LogWarning("Reserved Slots Config Error: Maximum slots is set to something higher than 48, limiting to 48.");
                    ConfigurationInstance.MaxSlotCount = 48;
                }
                if (ConfigurationInstance.MaxSlotCount < ConfigurationInstance.MinSlotCount)
                {
                    Logger.LogError("Reserved Slots Config Error: Max slot count is less than initial slot count, Setting max slot count to min slot count + reserved slots, or max slot count, if over 48.");
                    byte tmp = (byte)(ConfigurationInstance.MinSlotCount + ConfigurationInstance.ReservedSlotCount);
                    ConfigurationInstance.MaxSlotCount = tmp > ConfigurationInstance.MaxSlotCount ? ConfigurationInstance.MaxSlotCount : tmp;
                }
                SetMaxPlayers();
            }

            SaveConfiguration();
        }

        private void SetMaxPlayers(bool onDisconnect = false)
        {
            // Minus one if it is coming from disconnect, they are still accounted towards the total player count at this time.
            int curPlayerNum = Provider.clients.Count - (onDisconnect ? 1 : 0);
            byte curPlayerMax = Provider.maxPlayers;
            if (curPlayerNum + ConfigurationInstance.ReservedSlotCount < ConfigurationInstance.MinSlotCount)
                curPlayerMax = ConfigurationInstance.MinSlotCount;
            else if (curPlayerNum + ConfigurationInstance.ReservedSlotCount > ConfigurationInstance.MaxSlotCount)
                curPlayerMax = ConfigurationInstance.MaxSlotCount;
            else if (curPlayerNum + ConfigurationInstance.ReservedSlotCount >= ConfigurationInstance.MinSlotCount && curPlayerNum + ConfigurationInstance.ReservedSlotCount <= ConfigurationInstance.MaxSlotCount)
            {
                curPlayerMax = (byte)(curPlayerNum + ConfigurationInstance.ReservedSlotCount);
            }
            if (_lastMaxSlotCount != curPlayerMax)
            {
                Provider.maxPlayers = curPlayerMax;
                _lastMaxSlotCount = curPlayerMax;
            }
        }

        private bool CheckReserved(UnturnedPlayer player)
        {
            if (SteamAdminlist.checkAdmin(player.CSteamID))
            {
                return true;
            }

            var permissionProvider = Container.Resolve<IPermissionProvider>();

            foreach (var group in permissionProvider.GetGroups(player))
            {
                if (ConfigurationInstance.Groups.Contains(@group.Id))
                {
                    return true;
                }
            }
            return false;
        }

        public void HandleEvent(IEventEmitter emitter, UnturnedPlayerPreConnectEvent @event)
        {
            var player = @event.Player as UnturnedPlayer;
            if (player == null)
                return;

            if (!ConfigurationInstance.ReservedSlotEnable || ConfigurationInstance.ReservedSlotCount <= 0 ||
                ConfigurationInstance.Groups == null || ConfigurationInstance.Groups.Length <= 0) return;

            int numPlayers = Provider.clients.Count;
            byte maxPlayers = Provider.maxPlayers;

            var playerManager = Container.Resolve<IPlayerManager>();

            // Run slot fill calculations, if it is enabled.
            if (ConfigurationInstance.AllowFill)
            {
                foreach (var onlinePlayer in playerManager.OnlinePlayers)
                {
                    if (CheckReserved((UnturnedPlayer)onlinePlayer))
                    {
                        numPlayers--;
                    }
                }
            }

            // Check to see if dynamic slots are enabled, and adjust the max slot count on the server if they are.
            if ((!ConfigurationInstance.AllowDynamicMaxSlot && numPlayers + ConfigurationInstance.ReservedSlotCount >= maxPlayers) || (ConfigurationInstance.AllowDynamicMaxSlot && numPlayers + ConfigurationInstance.ReservedSlotCount >= ConfigurationInstance.MaxSlotCount))
            {
                // Kick if they aren't a reserved player.
                if (!CheckReserved(player))
                {
                    @event.IsCancelled = true;
                    @event.UnturnedRejectionReason = ESteamRejection.SERVER_FULL;
                }
            }
        }

        // Adjust the max player count on player connect and disconnect, if the dynamic slots feature is enabled.
        public void HandleEvent(IEventEmitter emitter, PlayerConnectedEvent @event)
        {
            if (!ConfigurationInstance.AllowDynamicMaxSlot)
                return;

            SetMaxPlayers();
        }

        public void HandleEvent(IEventEmitter emitter, PlayerDisconnectedEvent @event)
        {
            if (!ConfigurationInstance.AllowDynamicMaxSlot)
                return;

            SetMaxPlayers(true);
        }
    }
}
