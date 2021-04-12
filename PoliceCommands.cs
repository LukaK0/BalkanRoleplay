using GTANetworkAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buildings;
using character;
using chat;
using Data;
using Data.Persistent;
using Data.Temporary;
using factions;
using jobs;
using messages.arguments;
using messages.error;
using messages.general;
using messages.help;
using messages.information;
using Utility;
using vehicles;
using weapons;
using static Utility.Enumerators;

namespace Server.Commands
{
    public static class PoliceCommands
    {
        [Command]
        public static void CheckCommand(Player player)
        {
            if (Emergency.IsPlayerDead(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_is_dead);
                return;
            }

            if (!player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).OnDuty)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_on_duty);
                return;
            }

            if (!Faction.IsPoliceMember(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_police_faction);
                return;
            }

            Vehicle vehicle = Vehicles.GetClosestVehicle(player);

            if (vehicle == null || !vehicle.Exists)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_vehicles_near);
                return;
            }

            VehicleModel vehModel = Vehicles.GetVehicleById<VehicleModel>(vehicle.GetData<int>(EntityData.VehicleId));

            player.SendChatMessage(Constants.COLOR_INFO + string.Format(GenRes.vehicle_check_title, vehModel.Id));
            player.SendChatMessage(Constants.COLOR_INFO + GenRes.vehicle_model + Constants.COLOR_HELP + (VehicleHash)vehModel.Model);
            player.SendChatMessage(Constants.COLOR_INFO + GenRes.vehicle_plate + Constants.COLOR_HELP + vehModel.Plate);
            player.SendChatMessage(Constants.COLOR_INFO + GenRes.owner + Constants.COLOR_HELP + vehModel.Owner);

            string message = string.Format(InfoRes.check_vehicle_plate, player.Name, (VehicleHash)vehModel.Model);
            Chat.SendMessageToNearbyPlayers(player, message, ChatTypes.Me, 20.0f, true);
        }

        [Command]
        public static void FriskCommand(Player player, string targetString)
        {
            if (Emergency.IsPlayerDead(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_is_dead);
                return;
            }

            if (!player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).OnDuty)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_on_duty);
                return;
            }

            if (!Faction.IsPoliceMember(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_police_faction);
                return;
            }

            Player target = UtilityFunctions.GetPlayer(targetString);

            if (target == null || player.Position.DistanceTo(target.Position) > 2.5f)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_too_far);
                return;
            }

            if (target == player)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_searched_himself);
                return;
            }

            player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame).SearchedTarget = target;

            Chat.SendMessageToNearbyPlayers(player, string.Format(InfoRes.player_frisk, player.Name, target.Name), ChatTypes.Me, 20.0f, true);

            //prikazi inv pretresanog
            player.TriggerEvent("ShowPlayerInventory", InventoryTarget.Player, Inventory.GetEntityInventory(player), Inventory.GetEntityInventory(target, true));
        }


        [Command]
        public static void EquipmentCommand(Player player, string action, string type = "")
        {
            if (Emergency.IsPlayerDead(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_is_dead);
                return;
            }

            if (!Faction.IsPoliceMember(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_police_faction);
                return;
            }

            if (!Police.IsCloseToEquipmentLockers(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_in_room_lockers);
                return;
            }

            CharacterModel characterModel = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database);

            if (!characterModel.OnDuty)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_on_duty);
                return;
            }

            if (action.Equals(ArgRes.basic, StringComparison.InvariantCultureIgnoreCase))
            {
                Weapons.GivePlayerNewWeapon(player, WeaponHash.Flashlight, 0, false);
                Weapons.GivePlayerNewWeapon(player, WeaponHash.Nightstick, 0, true);
                Weapons.GivePlayerNewWeapon(player, WeaponHash.Stungun, 0, true);

                player.Armor = 100;

                player.SendChatMessage(Constants.COLOR_INFO + InfoRes.equip_basic_received);

                return;
            }

            if (action.Equals(ArgRes.ammunition, StringComparison.InvariantCultureIgnoreCase))
            {
                if (characterModel.Rank <= 1)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_enough_police_rank);
                    return;
                }

                string ammunition = Weapons.GetGunAmmunitionType(player.CurrentWeapon);

                if (ammunition == string.Empty)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.weapon_not_valid);
                    return;
                }

                GameItemModel bulletItem = Inventory.GetPlayerItemModelFromHash(characterModel.Id, ammunition);

                if (bulletItem != null)
                {
                    switch (player.CurrentWeapon)
                    {
                        case WeaponHash.Combatpistol:
                            bulletItem.Amount += (int)GunCapacity.Handguns;
                            break;

                        case WeaponHash.Smg:
                            bulletItem.Amount += (int)GunCapacity.Smg;
                            break;

                        case WeaponHash.Carbinerifle:
                            bulletItem.Amount += (int)GunCapacity.Assault_Rifles;
                            break;

                        case WeaponHash.Pumpshotgun:
                            bulletItem.Amount += (int)GunCapacity.Shotguns;
                            break;

                        case WeaponHash.Sniperrifle:
                            bulletItem.Amount += (int)GunCapacity.Sniper_Rifles;
                            break;
                    }

                    Task.Run(() => DatabaseOperations.UpdateItem(bulletItem)).ConfigureAwait(false);
                }
                else
                {
                    bulletItem = new GameItemModel()
                    {
                        Hash = ammunition,
                        OwnerEntity = ItemOwner.Player,
                        OwnerIdentifier = characterModel.Id,
                        Amount = 30,
                        Position = new Vector3(),
                        Dimension = 0
                    };

                    Task.Run(() => DatabaseOperations.AddNewItem(bulletItem)).ConfigureAwait(false);
                }

                player.SendChatMessage(Constants.COLOR_INFO + InfoRes.equip_ammo_received);

                return;
            }

            if (action.Equals(ArgRes.weapon, StringComparison.InvariantCultureIgnoreCase))
            {
                if (characterModel.Rank <= 1)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_enough_police_rank);
                    return;
                }

                if (string.IsNullOrEmpty(type))
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.equipment_weapon);
                    return;
                }

                WeaponHash selectedWeap;
                if (type.Equals(ArgRes.pistol, StringComparison.InvariantCultureIgnoreCase))
                {
                    selectedWeap = WeaponHash.Combatpistol;
                }
                else if (type.Equals(ArgRes.revolver, StringComparison.InvariantCultureIgnoreCase))
                {
                    selectedWeap = WeaponHash.Revolver;
                }
                else if (type.Equals(ArgRes.machinegun, StringComparison.InvariantCultureIgnoreCase))
                {
                    selectedWeap = WeaponHash.Smg;
                }
                else if (type.Equals(ArgRes.assault, StringComparison.InvariantCultureIgnoreCase))
                {
                    selectedWeap = WeaponHash.Carbinerifle;
                }
                else if (type.Equals(ArgRes.sniper, StringComparison.InvariantCultureIgnoreCase))
                {
                    selectedWeap = WeaponHash.Sniperrifle;
                }
                else if (type.Equals(ArgRes.shotgun, StringComparison.InvariantCultureIgnoreCase))
                {
                    selectedWeap = WeaponHash.Pumpshotgun;
                }
                else
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.equipment_weapon);
                    return;
                }

                Weapons.GivePlayerNewWeapon(player, selectedWeap, 0, true);
                player.SendChatMessage(Constants.COLOR_INFO + InfoRes.equip_weap_received);

                return;
            }

            player.SendChatMessage(Constants.COLOR_HELP + HelpRes.equipment);
        }

        [Command]
        public static void PutCommand(Player player, string item)
        {
            if (Emergency.IsPlayerDead(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_is_dead);
                return;
            }

            if (!player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).OnDuty)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_on_duty);
                return;
            }

            if (!Faction.IsPoliceMember(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_police_faction);
                return;
            }

            PoliceControlModel policeControl;

            if (item.Equals(ArgRes.cone, StringComparison.InvariantCultureIgnoreCase))
            {
                policeControl = new PoliceControlModel(0, string.Empty, PoliceControlItems.Cone, player.Position, player.Rotation);
                policeControl.Position = new Vector3(policeControl.Position.X, policeControl.Position.Y, policeControl.Position.Z - 1.0f);
                policeControl.ControlObject = NAPI.Object.CreateObject((int)PoliceControlItems.Cone, policeControl.Position, policeControl.Rotation);
                Police.policeControlList.Add(policeControl);
                return;
            }

            if (item.Equals(ArgRes.beacon, StringComparison.InvariantCultureIgnoreCase))
            {
                policeControl = new PoliceControlModel(0, string.Empty, PoliceControlItems.Beacon, player.Position, player.Rotation);
                policeControl.Position = new Vector3(policeControl.Position.X, policeControl.Position.Y, policeControl.Position.Z - 1.0f);
                policeControl.ControlObject = NAPI.Object.CreateObject((int)PoliceControlItems.Beacon, policeControl.Position, policeControl.Rotation);
                Police.policeControlList.Add(policeControl);
                return;
            }

            if (item.Equals(ArgRes.barrier, StringComparison.InvariantCultureIgnoreCase))
            {
                policeControl = new PoliceControlModel(0, string.Empty, PoliceControlItems.Barrier, player.Position, player.Rotation);
                policeControl.Position = new Vector3(policeControl.Position.X, policeControl.Position.Y, policeControl.Position.Z - 1.0f);
                policeControl.ControlObject = NAPI.Object.CreateObject((int)PoliceControlItems.Barrier, policeControl.Position, policeControl.Rotation);
                Police.policeControlList.Add(policeControl);
                return;
            }

            if (item.Equals(ArgRes.spikes, StringComparison.InvariantCultureIgnoreCase))
            {
                policeControl = new PoliceControlModel(0, string.Empty, PoliceControlItems.Spikes, player.Position, player.Rotation);
                policeControl.Position = new Vector3(policeControl.Position.X, policeControl.Position.Y, policeControl.Position.Z - 1.0f);
                policeControl.ControlObject = NAPI.Object.CreateObject((int)PoliceControlItems.Spikes, policeControl.Position, policeControl.Rotation);
                Police.policeControlList.Add(policeControl);
                return;
            }

            player.SendChatMessage(Constants.COLOR_HELP + HelpRes.put);
        }

        [Command]
        public static void LicenseCommand(Player player, string args)
        {
            if (!Faction.IsPoliceMember(player) || player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Rank != 6)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_police_chief);
                return;
            }

            string[] arguments = args.Trim().Split(' ');

            if (arguments.Length != 3 && arguments.Length != 4)
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.license);
                return;
            }

            string action = arguments[0];
            string item = arguments[1];

            arguments = arguments.Skip(2).ToArray();

            Player target = UtilityFunctions.GetPlayer(ref arguments);

            if (target == null || player.Position.DistanceTo(target.Position) > 2.5f)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_too_far);
                return;
            }

            if (action.Equals(ArgRes.give, StringComparison.InvariantCultureIgnoreCase))
            {
                if (!item.Equals(ArgRes.weapon, StringComparison.InvariantCultureIgnoreCase))
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.license);
                    return;
                }

                target.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).WeaponLicense = UtilityFunctions.GetTotalSeconds() + 2628000;

                player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.weapon_license_given, target.Name));
                target.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.weapon_license_received, player.Name));

                return;
            }

            if (action.Equals(ArgRes.remove, StringComparison.InvariantCultureIgnoreCase))
            {
                if (item.Equals(ArgRes.weapon, StringComparison.InvariantCultureIgnoreCase))
                {
                    target.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).WeaponLicense = UtilityFunctions.GetTotalSeconds();

                    player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.weapon_license_removed, target.Name));
                    target.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.weapon_license_lost, player.Name));

                    return;
                }

                if (item.Equals(ArgRes.car, StringComparison.InvariantCultureIgnoreCase))
                {
                    DrivingSchool.SetPlayerLicense(target, (int)DrivingLicenses.Car, -1);

                    player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.car_license_removed, target.Name));
                    target.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.car_license_lost, player.Name));

                    return;
                }

                if (item.Equals(ArgRes.motorcycle, StringComparison.InvariantCultureIgnoreCase))
                {
                    DrivingSchool.SetPlayerLicense(target, (int)DrivingLicenses.Motorcycle, -1);

                    player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.moto_license_removed, target.Name));
                    target.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.moto_license_lost, player.Name));

                    return;
                }

                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.license);
                return;
            }
        }
    }
}
