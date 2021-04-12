using GTANetworkAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildings.Businesses;
using character;
using chat;
using Currency;
using Data;
using Data.Persistent;
using Data.Temporary;
using factions;
using messages.arguments;
using messages.error;
using messages.general;
using messages.help;
using messages.information;
using messages.success;
using Utility;
using vehicles;
using weapons;
using static Utility.Enumerators;

namespace Server.Commands
{
    public static class VehiclesCommands
    {
        [Command]
        public static void SeatbeltCommand(Player player)
        {
            if (!player.IsInVehicle)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_in_vehicle);
                return;
            }

            player.TriggerEvent("toggleSeatbelt");
        }

        [Command]
        public static void LockCommand(Player player)
        {
            if (Emergency.IsPlayerDead(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_is_dead);
                return;
            }

            Vehicle vehicle = Vehicles.GetClosestVehicle(player);

            if (vehicle == null || !vehicle.Exists)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_vehicles_near);
                return;
            }

            if (!Vehicles.HasPlayerVehicleKeys(player, vehicle))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_car_keys);
                return;
            }

            if (vehicle.Class == (int)VehicleClass.Cycle || vehicle.Class == (int)VehicleClass.Motorcycle || vehicle.Class == (int)VehicleClass.Boat)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_not_lockable);
                return;
            }

            vehicle.Locked = !vehicle.Locked;
            Chat.SendMessageToNearbyPlayers(player, vehicle.Locked ? SuccRes.veh_locked : SuccRes.veh_unlocked, ChatTypes.Me, 20.0f);
        }

        [Command]
        public static void HoodCommand(Player player)
        {
            if (Emergency.IsPlayerDead(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_is_dead);
                return;
            }

            Vehicle vehicle = Vehicles.GetClosestVehicle(player);

            if (vehicle == null || !vehicle.Exists)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_vehicles_near);
                return;
            }

            if (!Vehicles.HasPlayerVehicleKeys(player, vehicle) && player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Job != PlayerJobs.Mechanic)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_car_keys);
                return;
            }

            int door = (int)VehicleDoor.Hood;
            List<bool> doorState = NAPI.Util.FromJson<List<bool>>(vehicle.GetSharedData<string>(EntityData.VehicleDoorsState));

            doorState[door] = !doorState[door];
            vehicle.SetSharedData(EntityData.VehicleDoorsState, NAPI.Util.ToJson(doorState));

            player.SendChatMessage(Constants.COLOR_INFO + (doorState[door] ? InfoRes.hood_opened : InfoRes.hood_closed));

            player.TriggerEvent("toggleVehicleDoor", vehicle.Value, door, doorState[door]);
        }


        [Command]
        public static void KeysCommand(Player player, string action, int vehicleId, string targetString = "")
        {
            CharacterModel characterModel = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database);

            string[] playerKeysArray = characterModel.VehicleKeys.Split(',');

            if (action.Equals(ArgRes.lend, StringComparison.InvariantCultureIgnoreCase))
            {
                VehicleModel vehicle = Vehicles.GetVehicleById<VehicleModel>(vehicleId);

                if (vehicle == null)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_not_exists);
                    return;
                }

                if (!Vehicles.HasPlayerVehicleKeys(player, vehicle))
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_car_keys);
                    return;
                }

                if (targetString.Length == 0)
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.keys);
                    return;
                }

                Player target = UtilityFunctions.GetPlayer(targetString);

                if (target == null || target.Position.DistanceTo(player.Position) > 5.0f)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_too_far);
                    return;
                }

                CharacterModel targetCharacterModel = target.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database);

                string[] targetKeysArray = targetCharacterModel.VehicleKeys.Split(',');

                for (int i = 0; i < targetKeysArray.Length; i++)
                {
                    if (int.Parse(targetKeysArray[i]) == 0)
                    {
                        targetKeysArray[i] = vehicleId.ToString();
                        targetCharacterModel.VehicleKeys = string.Join(",", targetKeysArray);

                        player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.vehicle_keys_given, target.Name));
                        target.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.vehicle_keys_received, player.Name));

                        return;
                    }
                }

                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_keys_full);

                return;
            }

            if (action.Equals(ArgRes.drop, StringComparison.InvariantCultureIgnoreCase))
            {
                for (int i = 0; i < playerKeysArray.Length; i++)
                {
                    if (playerKeysArray[i] == vehicleId.ToString())
                    {
                        playerKeysArray[i] = "0";

                        Array.Sort(playerKeysArray);
                        Array.Reverse(playerKeysArray);

                        characterModel.VehicleKeys = string.Join(',', playerKeysArray);

                        player.SendChatMessage(Constants.COLOR_INFO + InfoRes.vehicle_keys_thrown);
                        return;
                    }
                }

                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_car_keys);
            }

            player.SendChatMessage(Constants.COLOR_HELP + HelpRes.keys);
        }

        [Command]
        public static void LocateCommand(Player player, int vehicleId)
        {
            VehicleModel vehicle = Vehicles.GetVehicleById<VehicleModel>(vehicleId);

            if (vehicle == null)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_not_exists);
                return;
            }

            if (!Vehicles.HasPlayerVehicleKeys(player, vehicle))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_car_keys);
                return;
            }

            if (vehicle.Parking == 0)
            {
                player.TriggerEvent("locateVehicle", vehicle.Position);
            }
            else
            {
                ParkingModel parking = Parking.GetParkingById(vehicle.Parking);

                player.TriggerEvent("locateVehicle", parking.Position);
            }

            player.SendChatMessage(Constants.COLOR_INFO + InfoRes.vehicle_parked);
        }

        [Command]
        public static void RefuelCommand(Player player, int amount)
        {
            foreach (BusinessModel business in Business.BusinessCollection.Values)
            {
                if ((BusinessTypes)business.Ipl.Type == BusinessTypes.GasStation && player.Position.DistanceTo(business.Entrance) < 20.5f)
                {
                    Vehicle vehicle = Vehicles.GetClosestVehicle(player);

                    if (vehicle == null || !vehicle.Exists)
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_vehicles_near);
                        return;
                    }

                    if (player.Vehicle != null)
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_refuel_into_vehicle);
                        return;
                    }

                    if (!Vehicles.HasPlayerVehicleKeys(player, vehicle))
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_car_keys);
                        return;
                    }

                    if (vehicle.HasData(EntityData.VehicleRefueling))
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_refueling);
                        return;
                    }

                    PlayerTemporaryModel playerModel = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

                    if (playerModel.Refueling != null && playerModel.Refueling.Exists)
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_refueling);
                        return;
                    }

                    if (vehicle.EngineStatus)
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.engine_on);
                        return;
                    }

                    VehicleModel vehModel = Vehicles.GetVehicleById<VehicleModel>(vehicle.GetData<int>(EntityData.VehicleId));

                    if (vehModel.Gas == 50.0f)
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_tank_full);
                        return;
                    }

                    float gasRefueled = 0.0f;
                    float gasLeft = 50.0f - vehModel.Gas;
                    int maxMoney = (int)Math.Ceiling(gasLeft * (int)Prices.Gas * business.Multiplier);

                    if (amount == 0 || amount > maxMoney)
                    {
                        amount = maxMoney;
                        gasRefueled = gasLeft;
                    }
                    else if (amount > 0)
                    {
                        gasRefueled = amount / ((int)Prices.Gas * business.Multiplier);
                    }

                    if (!Money.SubstractPlayerMoney(player, amount, out string error))
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + error);
                        return;
                    }

                    vehModel.Gas += gasRefueled;

                    playerModel.Refueling = vehicle;
                    vehicle.SetData(EntityData.VehicleRefueling, player);

                    Timer gasTimer = new Timer(Vehicles.OnVehicleRefueled, vehicle, (int)Math.Round(gasLeft * 1000), Timeout.Infinite);
                    Vehicles.gasTimerList.Add(player.Value, gasTimer);

                    player.SendChatMessage(Constants.COLOR_INFO + InfoRes.vehicle_refueling);

                    return;
                }
            }

            player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_fuel_station_near);
        }

        [Command]
        public static void FillCommand(Player player)
        {
            if (!player.HasSharedData(EntityData.PlayerRightHand))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.right_hand_empty);
                return;
            }

            string rightHand = player.GetSharedData<string>(EntityData.PlayerRightHand);
            int itemId = NAPI.Util.FromJson<AttachmentModel>(rightHand).itemId;
            GameItemModel item = Inventory.GetItemModelFromId(itemId);

            if (item == null || item.Hash != Constants.ITEM_HASH_JERRYCAN)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_no_jerrycan);
                return;
            }

            Vehicle vehicle = Vehicles.GetClosestVehicle(player);

            if (vehicle == null || !vehicle.Exists)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_vehicles_near);
                return;
            }

            if (!Vehicles.HasPlayerVehicleKeys(player, vehicle) && player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Job != PlayerJobs.Mechanic)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_car_keys);
                return;
            }

            VehicleModel vehModel = Vehicles.GetVehicleById<VehicleModel>(vehicle.GetData<int>(EntityData.VehicleId));
            vehModel.Gas = (vehModel.Gas + Constants.GAS_CAN_LITRES > 50.0f ? 50.0f : vehModel.Gas + Constants.GAS_CAN_LITRES);

            NAPI.ClientEvent.TriggerClientEventInDimension(player.Dimension, "dettachItemFromPlayer", player.Value);
            player.ResetSharedData(EntityData.PlayerRightHand);

            Inventory.ItemCollection.Remove(item.Id);
            Task.Run(() => DatabaseOperations.DeleteSingleRow("items", "id", item.Id)).ConfigureAwait(false);

            player.SendChatMessage(Constants.COLOR_INFO + InfoRes.vehicle_refilled);
        }

        [Command]
        public static void VparkCommand(Player player)
        {
            int driving = UtilityFunctions.IsPlayerDriving(player);
            switch (driving)
            {
                case 1:
                    if (Vehicles.IsVehicleOwnedByPlayer(player))
                    {
                        if(!Vehicles.IsVehicleLowHP(player)) Administration.Admin.UpdateVehicleSpawn(player);
                        else player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_damaged);
                    }
                    else player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_veh_owner);
                    break;
                case 2:
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_vehicle_driving);
                    break;

                case 3:
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_in_vehicle);
                    break;

                case 4:
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_is_dead);
                    break;
            }

        }
    }
}
