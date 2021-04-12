using GTANetworkAPI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Buildings;
using Buildings.Businesses;
using Buildings.Houses;
using character;
using chat;
using Curency;
using Data;
using Data.Extended;
using Data.Persistent;
using Data.Temporary;
using drugs;
using factions;
using jobs;
using messages.general;
using messages.information;
using Utility;
using vehicles;
using weapons;
using static Utility.Enumerators;

namespace Server
{
    public class ServerHandler : Script
    {
        public static bool LimitedBlips;

        private Timer MinuteTimer;
        private Timer PlayerUpdateTimer;


        [ServerEvent(Event.PlayerEnterVehicle)]
        public void PlayerEnterVehicleEvent(Player player, Vehicle vehicle, sbyte seat)
        {
            VehicleModel vehModel = Vehicles.GetVehicleById<VehicleModel>(vehicle.GetData<int>(EntityData.VehicleId));

            if (vehModel.Faction == (int)PlayerFactions.DrivingSchool)
            {
                // Polaganje?
                DrivingSchool.OnPlayerEnterVehicle(player, vehicle);
                return;
            }

            CharacterModel characterModel = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database);

            switch (characterModel.Job)
            {
                case PlayerJobs.Fastfood:
                    FastFood.OnPlayerEnterVehicle(player, vehicle, vehModel.Faction);
                    break;

                case PlayerJobs.GarbageCollector:
                    Garbage.OnPlayerEnterVehicle(player, vehicle, seat, vehModel.Faction);
                    break;
            }

            if (seat == (sbyte)VehicleSeat.Driver)
            {
                Taxi.OnPlayerEnterVehicle(player, vehicle);
                Weapons.OnPlayerEnterVehicle(player, vehicle, vehModel.Id);
                Vehicles.OnPlayerEnterVehicle(player, vehicle, vehModel);
            }
        }

        [ServerEvent(Event.Update)]
        public void OnUpdate()
        {

        }

        private void UpdatePlayerList(object unused)
        {
            List<ScoreModel> scoreList = new List<ScoreModel>();

            List<Player> playingPlayers = NAPI.Pools.GetAllPlayers().FindAll(p => Character.IsPlaying(p));

            NAPI.Task.Run(() =>
            {
                foreach (Player player in playingPlayers)
                {
                    ScoreModel score = new ScoreModel(player.Value, player.Name, player.Ping);
                    scoreList.Add(score);
                }

                foreach (Player p in playingPlayers) p.TriggerEvent("updatePlayerList", scoreList);
            });
        }

        private void OnMinuteSpent(object unused)
        {
            NAPI.Task.Run(() =>
            {
                //sinhronizacija
                TimeSpan currentTime = TimeSpan.FromTicks(DateTime.Now.Ticks);

                NAPI.World.SetTime(currentTime.Hours, currentTime.Minutes, currentTime.Seconds);

                NAPI.World.SetWeather(NAPI.World.GetWeather());

                //vehicle respawn
                List<Vehicle> allVeh = NAPI.Pools.GetAllVehicles();
                foreach (Vehicle veh in allVeh) if (NAPI.Vehicle.GetVehicleHealth(veh) < 1 && NAPI.Vehicle.GetVehicleEngineHealth(veh) < 1) Vehicles.RespawnVehicle(veh);
            });

            int totalSeconds = UtilityFunctions.GetTotalSeconds();
            Player[] onlinePlayers = NAPI.Pools.GetAllPlayers().Where(pl => Character.IsPlaying(pl)).ToArray();


            foreach (Player player in onlinePlayers)
            {
                CharacterModel characterModel = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database);
                PlayerTemporaryModel playerModel = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

                if (characterModel.Played > 0 && characterModel.Played % 60 == 0)
                {
                    if (characterModel.EmployeeCooldown > 0)
                    {
                        characterModel.EmployeeCooldown--;
                    }

                    NAPI.Task.Run(() => GeneratePlayerPayday(player));
                }

                characterModel.Played++;

                /*
                if (playerModel.TimeHospitalRespawn != 0 && playerModel.TimeHospitalRespawn <= totalSeconds)
                {
                    // Send the death warning
                    player.SendChatMessage(Constants.COLOR_INFO + InfoRes.player_can_die);
                }*/

                if (characterModel.JobCooldown > 0)
                {
                    characterModel.JobCooldown--;
                }

                if (playerModel.Jailed > 0)
                {
                    playerModel.Jailed--;
                }
                else if (playerModel.Jailed == 0)
                {
                    NAPI.Task.Run(() => player.Position = Coordinates.JailSpawns[playerModel.JailType == JailTypes.Ic ? 3 : 4]);

                    playerModel.JailType = JailTypes.None;
                    playerModel.Jailed = -1;

                    player.SendChatMessage(Constants.COLOR_INFO + InfoRes.player_unjailed);
                }

                if (playerModel.DrunkLevel > 0.0f)
                {
                    float drunkLevel = playerModel.DrunkLevel - 0.05f;

                    if (drunkLevel <= 0.0f)
                    {
                        playerModel.DrunkLevel = 0.0f;
                    }
                    else
                    {
                        if (drunkLevel < Constants.WASTED_LEVEL)
                        {
                            player.ResetSharedData(EntityData.PlayerWalkingStyle);
                            NAPI.ClientEvent.TriggerClientEventForAll("resetPlayerWalkingStyle", player.Handle);
                        }

                        playerModel.DrunkLevel -= drunkLevel;
                    }
                }

                NAPI.Task.Run(() =>
                {
                    characterModel.Position = playerModel.Spawn == null ? player.Position : playerModel.Spawn.Position;
                    characterModel.Rotation = playerModel.Spawn == null ? player.Rotation : new Vector3(0.0f, 0.0f, playerModel.Spawn.Heading);
                    characterModel.Health = player.Health;
                    characterModel.Armor = player.Armor;

                    Character.SaveCharacterData(characterModel);
                });
            }

            FastFood.RefreshOrders(totalSeconds);

            Hunter.RespawnDeathAnimals(totalSeconds);

            Drugs.UpdateGrowth();

            Vehicles.SaveAllVehicles();
        }
    }
}
