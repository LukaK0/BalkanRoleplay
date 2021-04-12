using GTANetworkAPI;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Administration;
using Buildings;
using Buildings.Businesses;
using Buildings.Houses;
using character;
using Currency;
using data.persistent;
using Data;
using Data.Persistent;
using Data.Temporary;
using factions;
using messages.administration;
using messages.arguments;
using messages.commands;
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
    public static class AdminCommands
    {
        [Command]
        public static void SkinCommand(Player player, string pedModel)
        {
            if (player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank > StaffRank.None)
            {
                PedHash pedHash = NAPI.Util.PedNameToModel(pedModel);
                player.SetSkin(pedHash);
            }
        }

        [Command]
        public static void AdminCommand(Player player, string message)
        {
            if (player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank > StaffRank.Support)
            {
                NAPI.Chat.SendChatMessageToAll(Constants.COLOR_ADMIN_INFO + GenRes.admin_notice + message);
            }
        }


        [Command]
        public static void TpCommand(Player player, string targetString)
        {
            if (player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank <= StaffRank.Support) return;

            Player target = int.TryParse(targetString, out int targetId) ? UtilityFunctions.GetPlayer(targetId) : NAPI.Player.GetPlayerFromName(targetString);

            if (!Character.IsPlaying(target))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_found);
                return;
            }

            BuildingHandler.RemovePlayerFromBuilding(player, target.Position, target.Dimension);

            if (BuildingHandler.IsIntoBuilding(target))
            {
                BuildingHandler.PlacePlayerIntoBuilding(target, player);
            }

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.goto_player, target.Name));
        }

        [Command]
        public static void BringCommand(Player player, string targetString)
        {
            if (player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank <= StaffRank.Support) return;

            Player target = UtilityFunctions.GetPlayer(targetString);

            if (!Character.IsPlaying(target))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_found);
                return;
            }

            BuildingHandler.RemovePlayerFromBuilding(target, player.Position, player.Dimension);

            if (BuildingHandler.IsIntoBuilding(player))
            {
                BuildingHandler.PlacePlayerIntoBuilding(player, target);
            }

            target.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.bring_player, player.SocialClubName));
        }

        [Command]
        public static void GunCommand(Player player, string targetString, string weaponName, int ammo)
        {
            if (player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank > StaffRank.GameMaster)
            {
                Player target = UtilityFunctions.GetPlayer(targetString);

                if (!Character.IsPlaying(target))
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_found);
                    return;
                }

                if (Inventory.HasPlayerItemOnHand(target))
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.target_right_hand_not_empty);
                    return;
                }

                WeaponHash weapon = NAPI.Util.WeaponNameToModel(weaponName);

                if (weapon == 0)
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.gun);
                }
                else
                {
                    Weapons.GivePlayerNewWeapon(target, weapon, ammo, false);
                }
            }
        }

        public static void VehCommand(Player player, string args)
        {
            StaffRank rank = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank;
            if (rank == StaffRank.None) return;
            if (args == null || args.Length == 0)//args.Trim().Length == 0)
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.veh);
                return;
            }


            if (rank <= StaffRank.GameMaster) return;

            Admin.CreateAdminVehicle1(player, args);

            return;


        }

        [Command]
        public static void VehicleCommand(Player player, string args)
        {
            StaffRank rank = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank;

            if (rank == StaffRank.None) return;

            if (args == null || args.Trim().Length == 0)
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.vehicle);
                return;
            }

            string[] arguments = args.Trim().Split(' ');

            if (arguments[0].Equals(ArgRes.info, StringComparison.InvariantCultureIgnoreCase))
            {
                if (rank <= StaffRank.Support) return;

                Admin.ShowVehicleInfo(player);

                return;
            }

            if (arguments[0].Equals(ArgRes.create, StringComparison.InvariantCultureIgnoreCase))
            {
                if (rank <= StaffRank.GameMaster) return;

                Admin.CreateAdminVehicle(player, arguments);

                return;
            }

            if (arguments[0].Equals(ArgRes.modify, StringComparison.InvariantCultureIgnoreCase))
            {
                if (arguments.Length == 1)
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.vehicle_modify);
                    return;
                }

                if (arguments[1].Equals(ArgRes.color, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (rank <= StaffRank.Support) return;

                    if (arguments.Length != 5 || !UtilityFunctions.CheckColorStructure(arguments[3]) || !UtilityFunctions.CheckColorStructure(arguments[4]) || !int.TryParse(arguments[2], out int vehicleId))
                    {
                        player.SendChatMessage(Constants.COLOR_HELP + HelpRes.vehicle_color);
                        return;
                    }

                    Admin.ModifyVehicleColor(player, vehicleId, arguments[3], arguments[4]);

                    return;
                }

                if (arguments[1].Equals(ArgRes.dimension, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (arguments.Length != 4 || !int.TryParse(arguments[2], out int vehicleId) || !uint.TryParse(arguments[3], out uint dimension))
                    {
                        player.SendChatMessage(Constants.COLOR_HELP + HelpRes.vehicle_dimension);
                        return;
                    }

                    Admin.ChangeVehicleDimension(player, vehicleId, dimension);

                    return;
                }

                if (arguments[1].Equals(ArgRes.faction, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (rank <= StaffRank.Support) return;

                    if (arguments.Length != 4 || !int.TryParse(arguments[3], out int faction) || !int.TryParse(arguments[2], out int vehicleId))
                    {
                        player.SendChatMessage(Constants.COLOR_HELP + HelpRes.vehicle_faction);
                        return;
                    }

                    Admin.ChangeVehicleFaction(player, vehicleId, faction);

                    return;
                }

                if (arguments[1].Equals(ArgRes.position, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (rank <= StaffRank.Support) return;

                    if (!player.IsInVehicle)
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_in_vehicle);
                        return;
                    }

                    Admin.UpdateVehicleSpawn(player);

                    return;
                }

                if (arguments[1].Equals(ArgRes.owner, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (rank <= StaffRank.Support) return;

                    if (arguments.Length != 5 || !int.TryParse(arguments[2], out int vehicleId))
                    {
                        player.SendChatMessage(Constants.COLOR_HELP + HelpRes.vehicle_owner);
                        return;
                    }

                    Admin.ChangeVehicleOwner(player, vehicleId, arguments[3] + " " + arguments[4]);

                    return;
                }

                if (arguments[1].Equals(ArgRes.gas, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (rank <= StaffRank.Support) return;

                    if (arguments.Length != 4 || !int.TryParse(arguments[2], out int vehicleId) || !float.TryParse(arguments[3], out float gas))
                    {
                        player.SendChatMessage(Constants.COLOR_HELP + HelpRes.vehicle_gas);
                        return;
                    }

                    Admin.ChangeVehicleGas(player, vehicleId, gas);

                    return;
                }

                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.vehicle_modify);
                return;
            }

            if (arguments[0].Equals(ArgRes.remove, StringComparison.InvariantCultureIgnoreCase))
            {
                if (rank <= StaffRank.GameMaster) return;

                if (arguments.Length != 2 || !int.TryParse(arguments[1], out int vehicleId))
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.vehicle_delete);
                    return;
                }

                Admin.RemoveVehicle(player, vehicleId);

                return;
            }

            if (arguments[0].Equals(ArgRes.repair, StringComparison.InvariantCultureIgnoreCase))
            {
                if (rank <= StaffRank.GameMaster) return;

                if (player.Vehicle == null || !player.Vehicle.Exists)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_in_vehicle);
                    return;
                }

                player.Vehicle.Repair();

                player.SendChatMessage(Constants.COLOR_ADMIN_INFO + AdminRes.vehicle_repaired);

                return;
            }

            if (arguments[0].Equals(ArgRes.lock_command, StringComparison.InvariantCultureIgnoreCase))
            {
                if (rank <= StaffRank.Support) return;

                Vehicle veh = Vehicles.GetClosestVehicle(player);

                if (veh == null || !veh.Exists)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_vehicles_near);
                    return;
                }

                veh.Locked = !veh.Locked;

                player.SendChatMessage(Constants.COLOR_ADMIN_INFO + (veh.Locked ? SuccRes.veh_locked : SuccRes.veh_unlocked));

                return;
            }

            if (arguments[0].Equals(ArgRes.start, StringComparison.InvariantCultureIgnoreCase))
            {
                if (rank <= StaffRank.Support) return;

                if (player.VehicleSeat != (int)VehicleSeat.Driver)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_vehicle_driving);
                    return;
                }

                player.Vehicle.EngineStatus = true;

                return;
            }

            if (arguments[0].Equals(ArgRes.bring, StringComparison.InvariantCultureIgnoreCase))
            {
                if (rank <= StaffRank.Support) return;

                if (arguments.Length != 2 || !int.TryParse(arguments[1], out int vehicleId))
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.vehicle_bring);
                    return;
                }

                Admin.BringVehicle(player, vehicleId);

                return;
            }

            if (arguments[0].Equals(ArgRes.tp, StringComparison.InvariantCultureIgnoreCase))
            {
                if (rank <= StaffRank.Support) return;

                if (arguments.Length != 2 || !int.TryParse(arguments[1], out int vehicleId))
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.vehicle_bring);
                    return;
                }

                Admin.MovePlayerToVehicle(player, vehicleId);

                return;
            }

            if (arguments[0].Equals(ArgRes.respawn, StringComparison.InvariantCultureIgnoreCase))
            {
                if (rank <= StaffRank.Support) return;

                if (arguments.Length != 2 || !int.TryParse(arguments[1], out int vehicleId))
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.vehicle_respawn);
                    return;
                }

                // respawn
                Vehicle veh = Vehicles.GetVehicleById<Vehicle>(vehicleId);
                //veh.Occupants.Cast<Player>().ToList().ForEach(p => p.WarpOutOfVehicle());
                //VehicleModel vehicleModel = Vehicles.GetVehicleById<VehicleModel>(veh.GetData<int>(EntityData.VehicleId));
                //veh.Delete();
                //Vehicles.CreateIngameVehicle(vehicleModel);
                Vehicles.RespawnVehicle(veh);

                return;
            }

            player.SendChatMessage(Constants.COLOR_HELP + HelpRes.vehicle);
        }

        public static string helpPlaces = "USAGE: /go [add | remove | ime lokacije]";

        [Command]
        public static void GoCommand(Player player, string args)
        {
            string lokacije = "Lokacije: ";
            foreach (string a in Coordinates.GoPlaces.Keys) { lokacije += a + ", "; }
            if (player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank == StaffRank.None) return;
            args = args.ToLower();
            if (args == null || args.Trim().Length == 0 || args.Length == 0)
            {
                player.SendChatMessage(Constants.COLOR_HELP + helpPlaces);
                player.SendChatMessage(Constants.COLOR_HELP + lokacije);
                return;
            }
            string[] arguments = args.Trim().Split(' ');

            if (arguments.Length == 1)
            {
                if (arguments[0].Equals(ArgRes.add, StringComparison.InvariantCultureIgnoreCase))
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.go_add);
                    return;
                }
                else if (arguments[0].Equals(ArgRes.remove, StringComparison.InvariantCultureIgnoreCase))
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.go_remove);
                    player.SendChatMessage(Constants.COLOR_HELP + lokacije);
                }
                else if (!arguments[0].Equals(ArgRes.add, StringComparison.InvariantCultureIgnoreCase) && !Coordinates.GoPlaces.ContainsKey(arguments[0].ToLowerInvariant()))
                {
                    player.SendChatMessage(Constants.COLOR_HELP + helpPlaces);
                    player.SendChatMessage(Constants.COLOR_HELP + lokacije);
                    return;
                }
                else
                {
                    BuildingHandler.RemovePlayerFromBuilding(player, Coordinates.GoPlaces[arguments[0]], 0);
                    return;
                }
            }

            if (arguments.Length == 2)
            {
                if (arguments[0].Equals(ArgRes.add, StringComparison.InvariantCultureIgnoreCase))
                {
                    Vector3 coord = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
                    LocationModel loc = new LocationModel(arguments[1], coord);
                    Coordinates.GoPlaces.Add(arguments[1], coord);
                    Task.Run(() => DatabaseOperations.AddGoPlace(loc));
                    player.SendChatMessage(Constants.COLOR_ADMIN_INFO + "Uspesno dodato");
                    return;
                }
                else if (arguments[0].Equals(ArgRes.remove, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (Coordinates.GoPlaces.ContainsKey(arguments[1]))
                    {
                        Coordinates.GoPlaces.Remove(arguments[1]);
                        Task.Run(() => DatabaseOperations.DeleteSingleRow("goplaces", "location", arguments[1])).ConfigureAwait(false);
                        player.SendChatMessage(Constants.COLOR_ADMIN_INFO + "Uspesno uklonjeno.");
                        return;
                    }
                    else player.SendChatMessage(Constants.COLOR_HELP + HelpRes.go_remove);

                }
            }
        }

        [Command]
        public static async Task BusinessCommand(Player player, string args)
        {
            StaffRank rank = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank;

            if (!Admin.HasUserCommandPermission(player, ComRes.business) && rank == StaffRank.None) return;

            if (args == null || args.Trim().Length == 0)
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.business);
                return;
            }

            BusinessModel business = new BusinessModel();
            string[] arguments = args.Trim().Split(' ');

            if (arguments[0].Equals(ArgRes.info, StringComparison.InvariantCultureIgnoreCase))
            {
                if (Business.GetClosestBusiness(player) == null) { player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_business_close); return; }
                Admin.ShowBusinessInfo(player, Business.GetClosestBusiness(player));
                return;
            }

            if (arguments[0].Equals(ArgRes.create, StringComparison.InvariantCultureIgnoreCase))
            {
                if (!Admin.HasUserCommandPermission(player, ComRes.business, ArgRes.create) && rank == StaffRank.None) return;

                if (arguments.Length == 3 && int.TryParse(arguments[1], out int type) && Enum.IsDefined(typeof(BusinessTypes), type) && (arguments[2] == ArgRes.inner || arguments[2] == ArgRes.outer))
                {
                    await Admin.CreateBusinessAsync(player, type, arguments[2]);
                }
                else
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.business_create);

                    player.SendChatMessage(Constants.COLOR_HELP + BuildingHandler.GetAvailableBusinesses());
                }

                return;
            }

            if (arguments[0].Equals(ArgRes.modify, StringComparison.InvariantCultureIgnoreCase))
            {
                if (!Admin.HasUserCommandPermission(player, ComRes.business, ArgRes.modify) && rank == StaffRank.None) return;
                business = Business.GetClosestBusiness(player);


                if (business == null)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_business_close);
                    return;
                }

                if (arguments.Length < 2)
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.business_modify);
                    return;
                }

                if (arguments[1].Equals(ArgRes.name, StringComparison.InvariantCultureIgnoreCase))
                {

                    if (arguments.Length <= 2)
                    {
                        player.SendChatMessage(Constants.COLOR_HELP + HelpRes.business_modify_name);
                        return;
                    }


                    Admin.UpdateBusinessName(player, business, string.Join(' ', arguments.Skip(2)));

                    return;
                }

                if (arguments[1].Equals(ArgRes.type, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (arguments.Length == 3 && int.TryParse(arguments[2], out int businessType) && Enum.IsDefined(typeof(BusinessTypes), businessType))
                    {
                        Admin.UpdateBusinessType(player, business, (BusinessTypes)businessType);
                    }
                    else
                    {
                        player.SendChatMessage(Constants.COLOR_HELP + HelpRes.business_create);

                        player.SendChatMessage(Constants.COLOR_HELP + BuildingHandler.GetAvailableBusinesses());
                    }

                    return;
                }

                return;
            }

            if (arguments[0].Equals(ArgRes.remove, StringComparison.InvariantCultureIgnoreCase))
            {
                if (rank <= StaffRank.GameMaster) return;

                business = Business.GetClosestBusiness(player);

                if (business == null)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_business_close);
                    return;
                }

                if (business.Ipl.Name.Length == 0)
                {
                    business.BusinessMarker.Delete();
                }
                else
                {
                    business.Label.Delete();
                }

                business.ColShape.Delete();

                await Task.Run(() => DatabaseOperations.DeleteSingleRow("business", "id", business.Id)).ConfigureAwait(false);
                Business.BusinessCollection.Remove(business.Id);

                return;
            }

            if (arguments[0].Equals(ArgRes.tp, StringComparison.InvariantCultureIgnoreCase))
            {
                if (rank <= StaffRank.Support) return;
                if (arguments.Length != 2 || !int.TryParse(arguments[1], out int businessId))
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.business_goto);
                    return;
                }

                business = Business.GetBusinessById(businessId);

                if (business == null)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.business_not_exists);
                    return;
                }

                BuildingHandler.RemovePlayerFromBuilding(player, business.Entrance, business.Dimension);

                return;
            }
            player.SendChatMessage(Constants.COLOR_HELP + HelpRes.business);
        }


        [Command]
        public static void ReviveCommand(Player player, string targetString)
        {
            if (player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank <= StaffRank.Support) return;

            Player target = UtilityFunctions.GetPlayer(targetString);

            if (!Character.IsPlaying(target))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_found);
                return;
            }

            if (!Emergency.IsPlayerDead(target))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_dead);
                return;
            }

            Emergency.CancelPlayerDeath(target);

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.player_revived, target.Name));
            target.SendChatMessage(Constants.COLOR_SUCCESS + string.Format(SuccRes.admin_revived, player.SocialClubName));
        }

        public static void MakeadminCommand(Player player, string args)
        {
            if (!Admin.HasUserCommandPermission(player, ComRes.makeadmin, "") && player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank < StaffRank.Administrator) { player.SendChatMessage(Constants.COLOR_HELP + "Oces kurac"); return; }

            string[] arguments = args.Trim().Split(' ');

            if (arguments.Length < 2)
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.makeadmin);
                return;
            }

            Player target = UtilityFunctions.GetPlayer(ref arguments);

            if (!Character.IsPlaying(target))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_found);
                return;
            }

            if (!int.TryParse(arguments[0], out int aLevel))
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.makeadmin);
                return;
            }

            CharacterModel characterModel = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database);
            StaffRank privRank;
            switch (aLevel)
            {
                case 0:
                    privRank = Enumerators.StaffRank.None;
                    characterModel.AdminName = "None";
                    break;
                case 1:
                    privRank = Enumerators.StaffRank.Support;
                    characterModel.AdminName = "Support";
                    break;
                case 2:
                    privRank = Enumerators.StaffRank.GameMaster;
                    characterModel.AdminName = "GameMaster";
                    break;
                case 3:
                    privRank = Enumerators.StaffRank.SuperGameMaster;
                    characterModel.AdminName = "SuperGameMaster";
                    break;
                case 4:
                    privRank = Enumerators.StaffRank.Administrator;
                    characterModel.AdminName = "Administrator";
                    break;
                default:
                    privRank = Enumerators.StaffRank.None;
                    characterModel.AdminName = "None";
                    break;
            }
            characterModel.AdminRank = privRank;
            Task.Run(() => DatabaseOperations.AdminUpdate(player.SocialClubName, aLevel, characterModel.AdminName)).ConfigureAwait(false);

        }

        [Command]
        public static void JailCommand(Player player, string args)
        {
            if (player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank <= StaffRank.Support) return;

            string[] arguments = args.Trim().Split(' ');

            if (arguments.Length < 3)
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.jail);
                return;
            }

            Player target = UtilityFunctions.GetPlayer(ref arguments);

            if (!Character.IsPlaying(target))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_found);
                return;
            }

            if (!int.TryParse(arguments[0], out int jailTime))
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.jail);
                return;
            }

            string reason = string.Join(" ", arguments.Where(w => w != arguments[0]).ToArray());

            if (reason == null || reason.Trim().Length == 0)
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.jail);
                return;
            }

            BuildingHandler.RemovePlayerFromBuilding(target, Coordinates.JailOoc, 0);

            PlayerTemporaryModel data = target.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);
            data.JailType = JailTypes.Ooc;
            data.Jailed = jailTime;

            NAPI.Chat.SendChatMessageToAll(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.player_jailed, target.Name, jailTime, reason));

            Task.Run(() => DatabaseOperations.AddAdminLog(player.SocialClubName, target.Name, "jail", jailTime, reason)).ConfigureAwait(false);
        }

        [Command]
        public static void HealthCommand(Player player, string args)
        {
            if (player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank <= StaffRank.GameMaster) return;

            string[] arguments = args.Trim().Split(' ');

            if (arguments.Length != 2 && arguments.Length != 3)
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.health);
                return;
            }
           
            Player target = UtilityFunctions.GetPlayer(ref arguments);

            if (!Character.IsPlaying(target))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_found);
                return;
            }

            if (!int.TryParse(arguments[0], out int health) || health < 0 || health > 100)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.health_value_not_correct);
                return;
            }

            target.Health = health;

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.player_health, target.Name, health));
            target.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.target_health, player.Name, health));
        }

        [Command]
        public static void SaveCommand(Player player)
        {
            if (player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank <= StaffRank.Support) return;

            string message = string.Empty;

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + AdminRes.save_start);

            DatabaseOperations.UpdateBusinesses(Business.BusinessCollection);
            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.save_business, Business.BusinessCollection.Count));

            // Cuvanje svih konektovanih
            List<Player> connectedPlayers = NAPI.Pools.GetAllPlayers().FindAll(pl => Character.IsPlaying(pl));
            foreach (Player target in connectedPlayers)
            {

                CharacterModel character = target.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database);
                PlayerTemporaryModel tempModel = target.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

                character.Position = tempModel.Spawn == null ? target.Position : tempModel.Spawn.Position;
                character.Rotation = tempModel.Spawn == null ? target.Rotation : new Vector3(0.0f, 0.0f, tempModel.Spawn.Heading);
                character.Health = target.Health;
                character.Armor = target.Armor;

                // U bazu cuva
                Character.SaveCharacterData(character);
            }

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + AdminRes.characters_saved);

            Vehicles.SaveAllVehicles();

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + AdminRes.vehicles_saved);

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + AdminRes.save_finish);
        }

        [Command]
        public static void PrikazisveCommand(Player player, string arg)
        {
            if (player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank == 0) return;
            if (arg.Equals(ArgRes.kuce, StringComparison.InvariantCultureIgnoreCase))
            {
                player.SendChatMessage(Constants.COLOR_INFO + "Sve kuce: ");
                foreach (HouseModel house in House.HouseCollection.Values) player.SendChatMessage(Constants.COLOR_HELP + "Id: " + house.Id + ", vlasnik: " + house.Owner);
                return;
            }
            if (arg.Equals(ArgRes.firme, StringComparison.InvariantCultureIgnoreCase))
            {
                player.SendChatMessage(Constants.COLOR_INFO + "Sve firme: ");
                foreach (BusinessModel business in Business.BusinessCollection.Values) player.SendChatMessage(Constants.COLOR_HELP + "Id: " + business.Id + ", Ime: " + business.Caption);
                return;
            }
            if (arg.Equals(ArgRes.vozila, StringComparison.InvariantCultureIgnoreCase))
            {
                player.SendChatMessage(Constants.COLOR_INFO + "Sva vozila: ");
                foreach (VehicleModel vehicle in Vehicles.IngameVehicles.Values) player.SendChatMessage(Constants.COLOR_HELP + "Id: " + vehicle.Id + ", model:  " + NAPI.Vehicle.GetVehicleDisplayName((VehicleHash)vehicle.Model) + " " + ", vlasnik: " + vehicle.Owner + ", faction: " + vehicle.Faction);
                return;
            }
            if (arg.Equals(ArgRes.enterijere, StringComparison.InvariantCultureIgnoreCase))
            {
                player.SendChatMessage(Constants.COLOR_INFO + "Svi enterijeri: ");
                foreach (InteriorModel interior in GenericInterior.InteriorCollection.Values) player.SendChatMessage(Constants.COLOR_HELP + "Id: " + interior.Id + ", Ime:  " + interior.Caption);
                return;
            }
            player.SendChatMessage(Constants.COLOR_HELP + HelpRes.prikazisve);
        }
    }
}
