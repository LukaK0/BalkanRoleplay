using GTANetworkAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buildings;
using Buildings.Businesses;
using Buildings.Houses;
using character;
using chat;
using Currency;
using Data;
using Data.Persistent;
using Data.Temporary;
using factions;
using jobs;
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
    public static class UtilityCommands
    {
        [Command]
        public static void StoreCommand(Player player)
        {
            if (!Inventory.HasPlayerItemOnHand(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.right_hand_empty);
                return;
            }

            Inventory.StoreItemOnHand(player);
        }

        [Command]
        public static void ConsumeCommand(Player player)
        {
            if (!player.HasSharedData(EntityData.PlayerRightHand))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.right_hand_empty);
                return;
            }

            string rightHand = player.GetSharedData<string>(EntityData.PlayerRightHand);
            int itemId = NAPI.Util.FromJson<AttachmentModel>(rightHand).itemId;
            GameItemModel item = Inventory.GetItemModelFromId(itemId);
            if (item == null) { player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.right_hand_empty); return; }
            BusinessItemModel businessItem = Business.GetBusinessItemFromHash(item.Hash);

            if (businessItem.Type == ItemTypes.Consumable)
            {
                Inventory.ConsumeItem(player, item, businessItem);
            }
            else
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.item_not_consumable);
            }
        }

        [Command]
        public static void PurchaseCommand(Player player, int amount = 0)
        {
            CharacterModel characterModel = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database);

            if (characterModel.BuildingEntered.Type == BuildingTypes.Business)
            {
                BusinessModel business = Business.GetBusinessById(characterModel.BuildingEntered.Id);

                switch (business.Ipl.Type)
                {
                    case BusinessTypes.Clothes:
                        if (!Customization.IsCustomCharacter(player))
                        {
                            player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_customizable_clothes);
                            return;
                        }

                        List<ClothesModel> clothes = Customization.GetPlayerClothes(characterModel.Id);

                        player.SendChatMessage(Constants.COLOR_INFO + InfoRes.about_complements);
                        player.SendChatMessage(Constants.COLOR_INFO + InfoRes.for_avoid_clipping1);
                        player.SendChatMessage(Constants.COLOR_INFO + InfoRes.for_avoid_clipping2);
                        player.TriggerEvent("showClothesBusinessPurchaseMenu", business.Caption, business.Multiplier, Faction.IsPoliceMember(player));
                        break;

                    case BusinessTypes.BarberShop:
                        if (!Customization.IsCustomCharacter(player))
                        {
                            player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_customizable_barber);
                            return;
                        }

                        player.TriggerEvent("showHairdresserMenu", characterModel.Sex, NAPI.Util.ToJson(characterModel.Skin), business.Caption);
                        break;

                    case BusinessTypes.TattooShop:
                        if (!Customization.IsCustomCharacter(player))
                        {
                            player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_customizable_tattoo);
                            return;
                        }

                        Customization.RemovePlayerClothes(player, characterModel.Sex, true);

                        List<TattooModel> tattooList = Customization.tattooList.Where(t => t.player == characterModel.Id).ToList();

                        player.TriggerEvent("showTattooMenu", characterModel.Sex, tattooList, Constants.TATTOO_LIST, business.Caption, business.Multiplier);

                        break;

                    default:
                        List<BusinessItemModel> businessItems = Business.GetBusinessSoldItems(Convert.ToInt32(business.Ipl.Type));

                        if (businessItems.Count > 0)
                        {
                            player.TriggerEvent("showBusinessPurchaseMenu", businessItems, business.Caption, business.Multiplier);
                        }

                        break;
                }
            }
            else
            {
                foreach (HouseModel house in House.HouseCollection.Values)
                {
                    if (player.Position.DistanceTo(house.Entrance) <= 1.5f && player.Dimension == house.Dimension)
                    {
                        House.BuyHouse(player, house);
                        return;
                    }
                }

                foreach (ParkingModel parking in Parking.ParkingList.Values)
                {
                    if (player.Position.DistanceTo(parking.Position) < 2.5f && parking.Type == ParkingTypes.Scrapyard)
                    {
                        if (!Money.SubstractPlayerMoney(player, amount, out string error))
                        {
                            player.SendChatMessage(Constants.COLOR_ERROR + error);
                            return;
                        }

                        GameItemModel item = Inventory.GetPlayerItemModelFromHash(characterModel.Id, Constants.ITEM_HASH_BUSINESS_PRODUCTS);

                        if (item == null)
                        {
                            item = new GameItemModel
                            {
                                Amount = amount,
                                Dimension = 0,
                                Position = new Vector3(),
                                Hash = Constants.ITEM_HASH_BUSINESS_PRODUCTS,
                                OwnerEntity = ItemOwner.Player,
                                OwnerIdentifier = characterModel.Id,
                                ObjectHandle = null
                            };

                            Task.Run(() => DatabaseOperations.AddNewItem(item)).ConfigureAwait(false);
                        }
                        else
                        {
                            item.Amount += amount;

                            Task.Run(() => DatabaseOperations.UpdateItem(item)).ConfigureAwait(false);
                        }

                        player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.products_bought, amount, amount));

                        return;
                    }
                }
            }

        }

        [Command]
        public static void SellCommand(Player player, string args)
        {
            string[] arguments = args.Split(' ');

            if (arguments == null || arguments.Length == 0)
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.sell);
                return;
            }

            string action = arguments[0];
            arguments = arguments.Where(w => w != arguments[0]).ToArray();

            if (action.Equals(ArgRes.vehicle, StringComparison.InvariantCultureIgnoreCase))
            {
                if (arguments.Length < 3 || !int.TryParse(arguments[0], out int objectId))
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.sell_vehicle);
                    return;
                }

                arguments = arguments.Skip(1).ToArray();

                Player target = UtilityFunctions.GetPlayer(ref arguments);

                if (target == null || target == player || player.Position.DistanceTo(target.Position) > 5.0f)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_too_far);
                    return;
                }

                if (arguments.Length == 0 || !int.TryParse(arguments[0], out int price))
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.sell_vehicle);
                    return;
                }

                if (price <= 0)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.money_amount_positive);
                    return;
                }

                VehicleModel vehModel = Vehicles.GetVehicleById<VehicleModel>(objectId);

                if (vehModel == null)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_not_exists);
                    return;
                }

                if (vehModel.Owner != player.Name)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_veh_owner);
                    return;
                }

                PlayerTemporaryModel data = target.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

                data.JobPartner = player;
                data.SellingPrice = price;
                data.SellingVehicle = objectId;

                player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.vehicle_sell, vehModel.Model, target.Name, price));
                target.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.vehicle_sold, player.Name, vehModel.Model, price));

                return;
            }

            if (action.Equals(ArgRes.house, StringComparison.InvariantCultureIgnoreCase))
            {
                if (!int.TryParse(arguments[0], out int objectId))
                {
                    player.SendChatMessage(Constants.COLOR_HELP + HelpRes.sell_house);
                    return;
                }

                HouseModel house = House.GetHouseById(objectId);

                if (house == null)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.house_not_exists);
                    return;
                }

                if (house.Owner != player.Name)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_house_owner);
                    return;
                }

                Player[] playerArray = NAPI.Pools.GetAllPlayers().Where(p => Character.IsPlaying(p) && BuildingHandler.IsIntoBuilding(p)).ToArray();

                foreach (Player target in playerArray)
                {
                    BuildingModel building = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).BuildingEntered;

                    if (building.Type == BuildingTypes.House && building.Id == house.Id)
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.house_occupied);
                        return;
                    }
                }

                if (arguments.Length == 1)
                {
                    player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame).SellingHouseState = objectId;

                    int sellValue = (int)Math.Round(house.Price * Constants.HOUSE_SALE_STATE);
                    player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.house_sell_state, sellValue));
                }
                else
                {
                    arguments = arguments.Skip(1).ToArray();

                    Player target = UtilityFunctions.GetPlayer(ref arguments);

                    if (target == null || target == player || player.Position.DistanceTo(target.Position) > 5.0f)
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_too_far);
                        return;
                    }

                    if (arguments.Length == 0 || !int.TryParse(arguments[0], out int price) || price <= 0)
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.money_amount_positive);
                        return;
                    }

                    PlayerTemporaryModel data = target.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

                    data.JobPartner = player;
                    data.SellingPrice = price;
                    data.SellingHouse = objectId;

                    player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.house_sell, target.Name, price));
                    target.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.house_sold, player.Name, price));
                }

                return;
            }

            if (action.Equals(ArgRes.fish, StringComparison.InvariantCultureIgnoreCase))
            {
                BuildingModel building = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).BuildingEntered;

                if (building.Type != BuildingTypes.Business)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_fishing_business);
                    return;
                }

                BusinessModel business = Business.GetBusinessById(building.Id);

                if (business == null || (BusinessTypes)business.Ipl.Type != BusinessTypes.Fishing)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_fishing_business);
                    return;
                }

                GameItemModel fishModel = Inventory.GetPlayerItemModelFromHash(player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Id, Constants.ITEM_HASH_FISH);

                if (fishModel == null)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_fish_sellable);
                    return;
                }

                int amount = (int)Math.Round(fishModel.Amount * (int)Prices.Fish / 1000.0d);

                Inventory.ItemCollection.Remove(fishModel.Id);
                Task.Run(() => DatabaseOperations.DeleteSingleRow("items", "id", fishModel.Id)).ConfigureAwait(false);

                Money.GivePlayerMoney(player, amount, out string error);
                player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.fishing_won, amount));

                return;
            }

            player.SendChatMessage(Constants.COLOR_HELP + HelpRes.sell);
        }

        [Command]
        public static void HelpCommand(Player player)
        {
            player.TriggerEvent("ShowHelpWindow", player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).AdminRank != StaffRank.None);
        }

        [Command]
        public static void ShowCommand(Player player, string args)
        {
            if (Emergency.IsPlayerDead(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_is_dead);
                return;
            }

            string[] arguments = args.Trim().Split(' ');

            if (arguments.Length != 2 && arguments.Length != 3)
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.show);
                return;
            }

            Player target = UtilityFunctions.GetPlayer(ref arguments);

            if (target == null || player.Position.DistanceTo(target.Position) > 2.5f)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_too_far);
                return;
            }

            CharacterModel characterModel = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database);

            if (arguments[0].Equals(ArgRes.licenses, StringComparison.InvariantCultureIgnoreCase))
            {
                Chat.SendMessageToNearbyPlayers(player, string.Format(InfoRes.licenses_show, target.Name), ChatTypes.Me, 20.0f);

                DrivingSchool.ShowDrivingLicense(player, target);

                return;
            }

            if (arguments[0].Equals(ArgRes.insurance, StringComparison.InvariantCultureIgnoreCase))
            {
                if (characterModel.Insurance == 0)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_medical_insurance);
                    return;
                }

                DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                dateTime = dateTime.AddSeconds(characterModel.Insurance);

                Chat.SendMessageToNearbyPlayers(player, string.Format(InfoRes.insurance_show, target.Name), ChatTypes.Me, 20.0f);

                target.SendChatMessage(Constants.COLOR_INFO + GenRes.name + characterModel.RealName);
                target.SendChatMessage(Constants.COLOR_INFO + GenRes.age + characterModel.Age);
                target.SendChatMessage(Constants.COLOR_INFO + GenRes.sex + (characterModel.Sex == Sex.Male ? GenRes.SexMale : GenRes.SexFemale));
                target.SendChatMessage(Constants.COLOR_INFO + GenRes.expiry + dateTime.ToShortDateString());

                return;
            }

            if (arguments[0].Equals(ArgRes.insurance, StringComparison.InvariantCultureIgnoreCase))
            {
                if (characterModel.Documentation == 0)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_undocumented);
                    return;
                }

                Chat.SendMessageToNearbyPlayers(player, string.Format(InfoRes.identification_show, target.Name), ChatTypes.Me, 20.0f);

                target.SendChatMessage(Constants.COLOR_INFO + GenRes.name + characterModel.RealName);
                target.SendChatMessage(Constants.COLOR_INFO + GenRes.age + characterModel.Age);
                target.SendChatMessage(Constants.COLOR_INFO + GenRes.sex + (characterModel.Sex == Sex.Male ? GenRes.SexMale : GenRes.SexFemale));

                return;
            }

            player.SendChatMessage(Constants.COLOR_HELP + HelpRes.show);
        }

        [Command]
        public static void PayCommand(Player player, string args)
        {
            if (Emergency.IsPlayerDead(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_is_dead);
                return;
            }

            string[] arguments = args.Trim().Split(' ');

            if (arguments.Length != 2 && arguments.Length != 3)
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.pay);
                return;
            }

            Player target = UtilityFunctions.GetPlayer(ref arguments);

            if (target == null || player.Position.DistanceTo(target.Position) > 2.5f)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_too_far);
                return;
            }

            if (target == player)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_cant_self);
                return;
            }

            if (arguments.Length == 0 || !int.TryParse(arguments[0], out int amount) || amount < 0)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.money_amount_positive);
                return;
            }

            if (player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Money < amount)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_enough_money);
                return;
            }

            PlayerTemporaryModel data = target.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

            data.Payment = player;
            data.SellingPrice = amount;

            player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.payment_offer, amount, target.Name));
            target.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.payment_received, player.Name, amount));
        }

        [Command]
        public static void GiveCommand(Player player, string targetString)
        {
            if (!Inventory.HasPlayerItemOnHand(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.right_hand_empty);
                return;
            }

            Player target = UtilityFunctions.GetPlayer(targetString);

            if (target == null || player.Position.DistanceTo(target.Position) > 2.0f)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_too_far);
                return;
            }

            if (Inventory.HasPlayerItemOnHand(target))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.target_right_hand_not_empty);
                return;
            }

            GameItemModel item;
            string playerMessage = string.Empty;
            string targetMessage = string.Empty;

            if (player.HasSharedData(EntityData.PlayerRightHand))
            {
                string rightHand = player.GetSharedData<string>(EntityData.PlayerRightHand);
                item = Inventory.GetItemModelFromId(NAPI.Util.FromJson<AttachmentModel>(rightHand).itemId);

                BusinessItemModel businessItem = Business.GetBusinessItemFromHash(item.Hash);
                UtilityFunctions.AttachItemToPlayer(target, item.Id, NAPI.Util.GetHashKey(item.Hash), "IK_R_Hand", businessItem.Position, businessItem.Rotation, EntityData.PlayerRightHand);

                player.ResetSharedData(EntityData.PlayerRightHand);

                playerMessage = string.Format(InfoRes.item_given, businessItem.Description.ToLower(), target.Name);
                targetMessage = string.Format(InfoRes.item_received, player.Name, businessItem.Description.ToLower());
            }
            else
            {
                WeaponHash weaponHash = player.CurrentWeapon;
                item = Weapons.GetWeaponItem(player, weaponHash);

                target.GiveWeapon(weaponHash, 0);
                target.SetWeaponAmmo(weaponHash, item.Amount);

                player.RemoveWeapon(weaponHash);
                player.GiveWeapon(WeaponHash.Unarmed, 0);

                playerMessage = string.Format(InfoRes.item_given, item.Hash.ToLower(), target.Name);
                targetMessage = string.Format(InfoRes.item_received, player.Name, item.Hash.ToLower());
            }

            item.OwnerIdentifier = target.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Id;

            player.SendChatMessage(Constants.COLOR_INFO + playerMessage);
            target.SendChatMessage(Constants.COLOR_INFO + targetMessage);

            Task.Run(() => DatabaseOperations.UpdateItem(item)).ConfigureAwait(false);
        }

        [Command]
        public static void CancelCommand(Player player, string cancel)
        {
            PlayerTemporaryModel data = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

            if (cancel.Equals(ArgRes.interview, StringComparison.InvariantCultureIgnoreCase))
            {
                if (data.OnAir)
                {
                    data.OnAir = false;
                    player.SendChatMessage(Constants.COLOR_INFO + InfoRes.on_air_canceled);
                }

                return;
            }

            if (cancel.Equals(ArgRes.service, StringComparison.InvariantCultureIgnoreCase))
            {
                if (data.AlreadyFucking != null && data.AlreadyFucking.Exists)
                {
                    data.AlreadyFucking = null;
                    data.JobPartner = null;
                    data.HookerService = 0;
                    player.SendChatMessage(Constants.COLOR_INFO + InfoRes.hooker_service_canceled);
                }

                return;
            }

            if (cancel.Equals(ArgRes.money, StringComparison.InvariantCultureIgnoreCase))
            {
                if (data.Payment != null && data.Payment.Exists)
                {
                    data.Payment = null;
                    data.JobPartner = null;
                    player.SendChatMessage(Constants.COLOR_INFO + InfoRes.payment_canceled);
                }

                return;
            }

            if (cancel.Equals(ArgRes.order, StringComparison.InvariantCultureIgnoreCase))
            {
                if (data.DeliverOrder > 0)
                {
                    data.DeliverOrder = 0;
                    data.JobCheckPoint = 0;
                    data.LastVehicle = null;
                    data.JobWon = 0;

                    player.TriggerEvent("fastFoodDeliverFinished");

                    player.SendChatMessage(Constants.COLOR_INFO + InfoRes.deliverer_order_canceled);
                }

                return;
            }

            if (cancel.Equals(ArgRes.repaint, StringComparison.InvariantCultureIgnoreCase))
            {
                if (data.Repaint != null)
                {
                    VehicleModel vehModel = Vehicles.GetVehicleById<VehicleModel>(data.Repaint.Vehicle.GetData<int>(EntityData.VehicleId));

                    Mechanic.RepaintVehicle(data.Repaint.Vehicle, vehModel);

                    data.JobPartner.TriggerEvent("closeRepaintWindow");

                    data.JobPartner = null;
                    data.Repaint.Vehicle = null;
                    data.Repaint.ColorType = 0;
                    data.Repaint.FirstColor = string.Empty;
                    data.Repaint.SecondColor = string.Empty;
                    data.SellingPrice = 0;

                    player.SendChatMessage(Constants.COLOR_INFO + InfoRes.repaint_canceled);
                }

                return;
            }

            player.SendChatMessage(Constants.COLOR_HELP + HelpRes.cancel);
        }

        [Command]
        public static void PickUpCommand(Player player)
        {
            if (Inventory.HasPlayerItemOnHand(player))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.right_hand_occupied);
                return;
            }

            if (player.HasSharedData(EntityData.PlayerWeaponCrate))
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.both_hand_occupied);
                return;
            }

            GameItemModel itemGround = Inventory.GetClosestItem(player);
            if(itemGround == null) { player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_items_near); return; }
            GameItemModel playerItem = null;
            BusinessItemModel businessItem = Business.GetBusinessItemFromHash(itemGround.Hash);
            int playerId = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Id;


            if (itemGround != null)
            {
                if (!Enum.IsDefined(typeof(WeaponHash), itemGround.Hash))
                {
                    playerItem = Inventory.GetPlayerItemModelFromHash(playerId, itemGround.Hash); //ne racunajuci oruzje - jer ono ide na wheel
                    if (playerItem != null) //item postoji u inv
                    {
                        playerItem.Amount += itemGround.Amount; //spojicemo ga

                        //brisemo stari, jer smo ga spojili sa vec postojecim
                        Inventory.ItemCollection.Remove(itemGround.Id);
                        Task.Run(() => DatabaseOperations.DeleteSingleRow("items", "id", itemGround.Id)).ConfigureAwait(false);
                    }
                    else //isti item ne postoji u inv, podizemo bas njega
                    {
                        playerItem = itemGround;
                        playerItem.OwnerIdentifier = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Id;
                        playerItem.Position = new Vector3(0, 0, 0);
                    }
                }

                player.PlayAnimation("random@domestic", "pickup_low", 0);

                string hash = itemGround.Hash;

                if (Enum.IsDefined(typeof(WeaponHash), hash))
                {
                    Enum.TryParse(hash, out WeaponHash weapon);
                    bool pokupio = false;

                    if (Weapons.GetPlayerWeaponNames(player).Contains(weapon))
                    {
                        GameItemModel playerWeapon = Weapons.GetWeaponItem(player, weapon);
                        playerWeapon.Amount += itemGround.Amount;//ako vec ima to oruzje, povecamo mu municiju
                        player.SetWeaponAmmo(weapon, playerWeapon.Amount);
                        Task.Run(() => DatabaseOperations.UpdateItem(playerWeapon)).ConfigureAwait(false);

                        Inventory.ItemCollection.Remove(itemGround.Id); //brisemo oruzje sa poda, jer je spojeno sa postojecim
                        Task.Run(() => DatabaseOperations.DeleteSingleRow("items", "id", itemGround.Id)).ConfigureAwait(false);
                        pokupio = true;

                    }
                    else if (!Weapons.PlayerHasSameWeaponType(player, weapon) && !pokupio) //ako nema vec isto oruzje kod sebe, a ni oruzje istog tipa, dacemo mu ovo sa poda
                    {
                        player.GiveWeapon(weapon, 0);
                        player.SetWeaponAmmo(weapon, itemGround.Amount);
                        itemGround.OwnerEntity = ItemOwner.Wheel;
                        itemGround.OwnerIdentifier = playerId;
                        itemGround.Position = new Vector3(0, 0, 0);
                        Task.Run(() => DatabaseOperations.UpdateItem(itemGround)).ConfigureAwait(false);
                    }
                    else
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.weapon_slot_full);
                        return;
                    }
                }
                else
                {
                    playerItem.OwnerEntity = ItemOwner.RightHand;

                     UtilityFunctions.AttachItemToPlayer(player, playerItem.Id, NAPI.Util.GetHashKey(playerItem.Hash), "IK_R_Hand", businessItem.Position, businessItem.Rotation, EntityData.PlayerRightHand);
                          
                    Task.Run(() => DatabaseOperations.UpdateItem(playerItem)).ConfigureAwait(false);
                }
                itemGround.ObjectHandle.Delete();
                player.SendChatMessage(Constants.COLOR_INFO + InfoRes.player_picked_item);
            }
            else
            {
                WeaponCrateModel weaponCrate = Weapons.GetClosestWeaponCrate(player);

                if (weaponCrate == null)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_items_near);
                    return;
                }

                Weapons.PickUpCrate(player, weaponCrate);
            }
        }

        [Command]
        public static void DropCommand(Player player)
        {

            if (Inventory.HasPlayerItemOnHand(player))
            {
                //Da li u ruci drzi oruzje ili ne?
                GameItemModel item;
                if (Enum.IsDefined(typeof(WeaponHash), player.CurrentWeapon) && player.CurrentWeapon != WeaponHash.Unarmed) item = Weapons.GetWeaponItem(player, player.CurrentWeapon);
                else
                {
                    string rightHand = player.GetSharedData<string>(EntityData.PlayerRightHand);
                    item = Inventory.GetItemModelFromId(NAPI.Util.FromJson<AttachmentModel>(rightHand).itemId);
                }

                BusinessItemModel businessItem = Business.GetBusinessItemFromHash(item.Hash);

                Inventory.DropItem(player, item, businessItem, item.Amount, true);

                player.ResetSharedData(EntityData.PlayerRightHand);
            }
            else if (player.HasSharedData(EntityData.PlayerWeaponCrate))
            {
                WeaponCrateModel weaponCrate = Weapons.GetPlayerCarriedWeaponCrate(player.Value);

                if (weaponCrate != null)
                {
                    weaponCrate.Position = new Vector3(player.Position.X, player.Position.Y, player.Position.Z - 1.0f);
                    weaponCrate.CarriedEntity = string.Empty;
                    weaponCrate.CarriedIdentifier = 0;

                    weaponCrate.CrateObject = NAPI.Object.CreateObject(481432069, weaponCrate.Position, new Vector3(), 0);

                    UtilityFunctions.RemoveItemOnHands(player);
                    player.StopAnimation();

                    player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.player_inventory_drop, GenRes.weapon_crate));
                }
            }
            else
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.right_hand_empty);
            }
        }

        [Command]
        public static void DoorCommand(Player player)
        {
            BuildingModel building = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).BuildingEntered;

            foreach (HouseModel house in House.HouseCollection.Values)
            {
                if ((player.Position.DistanceTo(house.Entrance) <= 1.5f && player.Dimension == house.Dimension) || (building.Type == BuildingTypes.House && building.Id == house.Id))
                {
                    if (!House.HasPlayerHouseKeys(player, house))
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_house_owner);
                    }
                    else
                    {
                        house.Locked = !house.Locked;

                        Task.Run(() => DatabaseOperations.UpdateHouse(house)).ConfigureAwait(false);

                        player.SendChatMessage(Constants.COLOR_INFO + (house.Locked ? InfoRes.house_locked : InfoRes.house_opened));
                    }
                    return;
                }
            }

            foreach (BusinessModel business in Business.BusinessCollection.Values)
            {
                if ((player.Position.DistanceTo(business.Entrance) <= 1.5f && player.Dimension == business.Dimension) || (building.Type == BuildingTypes.Business && building.Id == business.Id))
                {
                    if (!Business.HasPlayerBusinessKeys(player, business))
                    {
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_not_business_owner);
                    }
                    else
                    {
                        business.Locked = !business.Locked;

                        Dictionary<int, BusinessModel> businessCollection = new Dictionary<int, BusinessModel>() { { business.Id, business } };
                        Task.Run(() => DatabaseOperations.UpdateBusinesses(businessCollection)).ConfigureAwait(false);

                        player.SendChatMessage(business.Locked ? Constants.COLOR_INFO + InfoRes.business_locked : Constants.COLOR_INFO + InfoRes.business_opened);
                    }
                    return;
                }
            }

            player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_house_business);
        }
    }
}
