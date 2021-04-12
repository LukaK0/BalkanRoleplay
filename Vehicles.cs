using GTANetworkAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using chat;
using Data;
using Data.Persistent;
using Data.Temporary;
using jobs;
using messages.error;
using messages.information;
using messages.success;
using Utility;
using static Utility.Enumerators;

namespace vehicles
{
    public class Vehicles : Script
    {
        public static Dictionary<int, Timer> gasTimerList;
        public static Dictionary<int, VehicleModel> IngameVehicles;

        public static void GenerateGameVehicles()
        {
            gasTimerList = new Dictionary<int, Timer>();

            foreach (VehicleModel vehModel in IngameVehicles.Values)
            {
                if (vehModel.Parking == 0)
                {
                    CreateIngameVehicle(vehModel);
                }
            }
        }

        public static async Task CreateVehicle(Player player, VehicleModel vehModel, bool adminCreated)
        {
            vehModel.Id = await DatabaseOperations.AddNewVehicle(vehModel);
            IngameVehicles.Add(vehModel.Id, vehModel);

            CreateIngameVehicle(vehModel);
            if (!adminCreated)
            {
                player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Bank -= vehModel.Price;
                player.SendChatMessage(Constants.COLOR_SUCCESS + string.Format(SuccRes.vehicle_purchased, vehModel.Model, vehModel.Price));
            }
        }

        public static void CreateIngameVehicle(VehicleModel vehModel)
        {
            Vehicle vehicle = NAPI.Vehicle.CreateVehicle(vehModel.Model, vehModel.Position, vehModel.Heading, 0, 0);
            vehicle.NumberPlate = vehModel.Plate == string.Empty ? "BG " + (1000 + vehModel.Id) : vehModel.Plate;
            vehicle.EngineStatus = vehModel.Engine != 0;
            vehicle.Locked = vehModel.Locked != 0;
            vehicle.Dimension = vehModel.Dimension;

            Mechanic.RepaintVehicle(vehicle, vehModel);

            if (vehicle.Model == (uint)VehicleHash.Ambulance)
            {
                vehicle.Livery = 1;
            }

            vehicle.SetData(EntityData.VehicleId, vehModel.Id);

            vehicle.SetSharedData(EntityData.VehicleSirenSound, true);
            vehicle.SetSharedData(EntityData.VehicleDoorsState, NAPI.Util.ToJson(new List<bool> { false, false, false, false, false, false }));

            Mechanic.AddTunningToVehicle(vehModel.Id, vehicle);
        }

        public static void OnPlayerDisconnected(Player player)
        {
            if (gasTimerList.TryGetValue(player.Value, out Timer gasTimer))
            {
                gasTimer.Dispose();
                gasTimerList.Remove(player.Value);
            }
        }

        public static Vehicle GetClosestVehicle(Player player)
        {
            double prevDist = 3.5;
            Vehicle prevVeh = null;

            foreach (Vehicle i in NAPI.Pools.GetAllVehicles())
            {
                if (!i.Exists) continue;
                double distBetween = Vector3.Distance(player.Position, i.Position);
                if (distBetween < prevDist)
                {
                    prevDist = distBetween;
                    prevVeh = i;
                }
            }
            return prevVeh == null ? null : prevVeh;
        }

        public static bool HasPlayerVehicleKeys(Player player, object veh)
        {
            VehicleModel vehicle = veh is Vehicle ? GetVehicleById<VehicleModel>(((Vehicle)veh).GetData<int>(EntityData.VehicleId)) : (VehicleModel)veh;

            if (vehicle.Testing) return false;

            bool hasKeys = false;

            CharacterModel characterModel = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database);

            if (vehicle.Owner == player.Name)
            {
                hasKeys = true;
            }
            else if (vehicle.Faction == (int)PlayerFactions.DrivingSchool)
            {
                hasKeys = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame).DrivingExam != DrivingExams.None;
            }
            else if (vehicle.Faction == (int)PlayerFactions.Admin)
            {
                hasKeys = characterModel.AdminRank > StaffRank.None;
            }
            else if (vehicle.Faction != (int)PlayerFactions.None)
            {
                hasKeys = (vehicle.Faction == (int)characterModel.Faction || (int)characterModel.Job + Constants.MAX_FACTION_VEHICLES == vehicle.Faction);
            }
            else
            {
                hasKeys = characterModel.VehicleKeys.Split(',').Any(key => int.Parse(key) == vehicle.Id);
            }

            return hasKeys;
        }

        public static T GetVehicleById<T>(int vehicleId)
        {
            if (typeof(VehicleModel).IsAssignableFrom(typeof(T)))
            {
                return IngameVehicles.ContainsKey(vehicleId) ? (T)Convert.ChangeType(IngameVehicles[vehicleId], typeof(T)) : default;
            }

            if (typeof(Vehicle).IsAssignableFrom(typeof(T)))
            {
                return (T)Convert.ChangeType(NAPI.Pools.GetAllVehicles().FirstOrDefault(veh => veh.GetData<int>(EntityData.VehicleId) == vehicleId), typeof(T));
            }

            return default;
        }

        public static void SaveAllVehicles()
        {
            Dictionary<int, VehicleModel> savedVehicles = IngameVehicles.Where(v => !v.Value.Testing && v.Value.Faction != (int)PlayerFactions.None)
                                                                        .ToDictionary(pair => pair.Key, pair => pair.Value);

            Task.Run(() => DatabaseOperations.SaveVehicles(savedVehicles)).ConfigureAwait(false);
        }

        public static bool IsVehicleTrunkInUse(Vehicle vehicle)
        {
            return NAPI.Pools.GetAllPlayers().Any(p => p.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame).OpenedTrunk == vehicle);
        }

        public static void RespawnVehicle(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists) return;

            vehicle.Repair();

            vehicle.Occupants.Cast<Player>().ToList().ForEach(p => p.WarpOutOfVehicle());
            VehicleModel vehicleModel = Vehicles.GetVehicleById<VehicleModel>(vehicle.GetData<int>(EntityData.VehicleId));
            if (vehicleModel.Faction >= 100 || vehicleModel.Faction==9) vehicleModel.Gas = 50;
            vehicle.Delete();
            Vehicles.CreateIngameVehicle(vehicleModel);
        }

        public static bool IsVehicleLowHP(Player player)
        {
            Vehicle vehicleClient = NAPI.Player.GetPlayerVehicle(player);
            if (NAPI.Vehicle.GetVehicleBodyHealth(vehicleClient) < 300 || NAPI.Vehicle.GetVehicleEngineHealth(vehicleClient) < 400) return true;
            return false;
        }
        public static bool IsVehicleOwnedByPlayer(Player player)
        {
            VehicleModel vehicle = Vehicles.GetVehicleById<VehicleModel>(player.Vehicle.GetData<int>(EntityData.VehicleId));
            if (player.Name.Equals(vehicle.Owner, StringComparison.InvariantCultureIgnoreCase)) return true;
            return false;
        }

        private void OnVehicleDeathTimer(Vehicle vehicle)
        {
            VehicleModel vehicleModel = GetVehicleById<VehicleModel>(vehicle.GetData<int>(EntityData.VehicleId));

            vehicle.Delete();

            CreateIngameVehicle(vehicleModel);
        }

        public static void OnVehicleRefueled(object vehicleObject)
        {
            Vehicle vehicle = (Vehicle)vehicleObject;
            Player player = vehicle.GetData<Player>(EntityData.VehicleRefueling);

            vehicle.ResetData(EntityData.VehicleRefueling);
            player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame).Refueling = null;

            if (gasTimerList.TryGetValue(player.Value, out Timer gasTimer))
            {
                gasTimer.Dispose();
                gasTimerList.Remove(player.Value);
            }

            player.SendChatMessage(Constants.COLOR_INFO + InfoRes.vehicle_refueled);
        }

        public static void OnPlayerEnterVehicle(Player player, Vehicle vehicle, VehicleModel vehModel)
        {
            if (vehModel.Testing)
            {
                Vehicle testingVehicle = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame).TestingVehicle;

                if (vehicle != testingVehicle)
                {
                    player.WarpOutOfVehicle();
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_testing_vehicle);
                    return;
                }
            }
            else if (vehModel.Faction != (int)PlayerFactions.None)
            {
                CharacterModel characterModel = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database);

                if (characterModel.AdminRank == StaffRank.None && vehModel.Faction == (int)PlayerFactions.Admin)
                {
                    player.WarpOutOfVehicle();
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.admin_vehicle);
                    return;
                }

                if (vehModel.Faction < Constants.MAX_FACTION_VEHICLES && (int)characterModel.Faction != vehModel.Faction && vehModel.Faction != (int)PlayerFactions.Admin)
                {
                    player.WarpOutOfVehicle();
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_in_vehicle_faction);
                    return;
                }

                if (vehModel.Faction > Constants.MAX_FACTION_VEHICLES && (int)characterModel.Job + Constants.MAX_FACTION_VEHICLES != vehModel.Faction)
                {
                    player.WarpOutOfVehicle();
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_in_vehicle_job);
                    return;
                }
            }

            if (vehicle.Class != (int)VehicleClass.Cycle)
            {
                player.SendChatMessage(Constants.COLOR_INFO + InfoRes.how_to_start_engine);

                player.TriggerEvent("initializeSpeedometer", vehModel.Kms, vehModel.Gas, vehicle.EngineStatus);
            }
        }

        [ServerEvent(Event.PlayerExitVehicle)]
        public void PlayerExitVehicletEvent(Player player, Vehicle vehicle)
        {
            NAPI.Task.Run(() => player.TriggerEvent("KeepVehicleEngineState", vehicle.Id, vehicle.EngineStatus), 500);
        }

        [ServerEvent(Event.VehicleDeath)]
        public void OnVehicleDeath(Vehicle vehicle)
        {
            vehicle.Occupants.Cast<Player>().ToList().ForEach(p => p.WarpOutOfVehicle());

            NAPI.Task.Run(() => OnVehicleDeathTimer(vehicle), 5000);
        }

        [RemoteEvent("stopPlayerCar")]
        public void StopPlayerCarEvent(Player player)
        {
            if (player.VehicleSeat == (int)VehicleSeat.Driver) player.Vehicle.EngineStatus = false;
        }

        [RemoteEvent("saveVehicleConsumes")]
        public void SaveVehicleConsumesEvent(Player _, ushort vehicleId, bool inWater, float kms, float gas)
        {
            Vehicle vehicle = NAPI.Pools.GetAllVehicles().FirstOrDefault(veh => Convert.ToInt32(vehicleId) == veh.Value);
            int id = vehicle.GetData<int>(EntityData.VehicleId);

            if (IngameVehicles.ContainsKey(id))
            {
                IngameVehicles[id].Kms = kms;
                IngameVehicles[id].Gas = gas;
            }

            if (inWater && vehicle.Class != (int)VehicleClass.Boat)
            {
                OnVehicleDeathTimer(vehicle);
            }
        }

        [RemoteEvent("toggleSeatbelt")]
        public void ToggleSeatbeltEvent(Player player, bool seatbelt)
        {
            Chat.SendMessageToNearbyPlayers(player, seatbelt ? InfoRes.seatbelt_fasten : InfoRes.seatbelt_unfasten, ChatTypes.Me, 20.0f);
        }
    }
}
