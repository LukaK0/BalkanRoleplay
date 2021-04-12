using GTANetworkAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using character;
using Data.Base;
using Data.Persistent;
using Data.Temporary;
using messages.error;
using static Utility.Enumerators;

namespace Utility
{
    public class UtilityFunctions : Script
    {
        public static Dictionary<int, OrderModel> truckerOrderCollection = new Dictionary<int, OrderModel>();

        public static Player GetPlayer(int playerId)
        {
            return NAPI.Pools.GetAllPlayers().FirstOrDefault(pl => Character.IsPlaying(pl) && pl.Value == playerId);
        }

        public static Player GetPlayer(string playerNameIdString)
        {
            return int.TryParse(playerNameIdString, out int targetId) ? GetPlayer(targetId) : NAPI.Player.GetPlayerFromName(playerNameIdString);
        }

        public static Player GetPlayer(ref string[] playerNameIdArray)
        {
            if (playerNameIdArray == null || playerNameIdArray.Length == 0) return null;

            if (playerNameIdArray.Length >= 1 && int.TryParse(playerNameIdArray[0], out int targetId))
            {
                playerNameIdArray = playerNameIdArray.Skip(1).ToArray();
                return GetPlayer(targetId);
            }

            if (playerNameIdArray.Length >= 2)
            {
                string name = playerNameIdArray[0] + " " + playerNameIdArray[1];
                playerNameIdArray = playerNameIdArray.Skip(2).ToArray();
                return NAPI.Player.GetPlayerFromName(name);
            }

            return null;
        }

        public static int GetTotalSeconds()
        {
            return (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        public static Vector3 GetForwardPosition(Entity entity, float distance)
        {
            Vector3 position = entity.Position;

            position.X += distance * (float)Math.Sin(-entity.Heading);
            position.Y += distance * (float)Math.Cos(-entity.Heading);

            return position;
        }

        public static void AttachItemToPlayer(Player player, int itemId, uint hash, string bodyPart, Vector3 position, Vector3 rotation, string entityKey)
        {
            AttachmentModel attachment = new AttachmentModel(itemId, hash, bodyPart, position, rotation);
            string attachmentJson = NAPI.Util.ToJson(attachment);

            player.SetSharedData(entityKey, attachmentJson);

            NAPI.ClientEvent.TriggerClientEventInDimension(player.Dimension, "attachItemToPlayer", player.Value, attachmentJson);
        }

        public static void RemoveItemOnHands(Player player)
        {
            if (player.HasSharedData(EntityData.PlayerRightHand))
            {
                player.ResetSharedData(EntityData.PlayerRightHand);
            }
            else if (player.HasSharedData(EntityData.PlayerWeaponCrate))
            {
                player.ResetSharedData(EntityData.PlayerWeaponCrate);
            }

            NAPI.ClientEvent.TriggerClientEventInDimension(player.Dimension, "dettachItemFromPlayer", player.Value);
        }

        public static bool CheckColorStructure(string color)
        {
            string[] colorArray = color.Split(',');

            if (colorArray.Length != 3) return false;

            foreach (string element in colorArray)
            {
                if (int.TryParse(element, out int colorCode) && colorCode >= 0 && colorCode <= 255) continue;
                return false;
            }

            return true;
        }

        public static int GetPlayerLevel(Player player)
        {
            float playedHours = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Played / 100;
            return (int)Math.Round(Math.Log(playedHours) * Constants.LEVEL_MULTIPLIER);
        }

        public static int IsPlayerDriving(Player player) //1 - vozi, 2 - nije vozac, 3 - nije u vozilu, 4 - mrtav
        {
            if (factions.Emergency.IsPlayerDead(player)) return 4;

            if (NAPI.Player.IsPlayerInAnyVehicle(player))
            {
                if (NAPI.Player.GetPlayerVehicleSeat(player) == 0) return 1;
                else return 2;
            }
            else return 3;
        }
    }
}
