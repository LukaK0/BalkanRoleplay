using GTANetworkAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buildings.Businesses;
using chat;
using Data;
using Data.Base;
using Data.Persistent;
using Data.Temporary;
using messages.arguments;
using messages.error;
using messages.information;
using Utility;
using weapons;

using static Utility.Enumerators;

namespace character
{
    public class Inventory : Script
    {
        public static Dictionary<int, GameItemModel> ItemCollection;

        public static void GenerateGroundItems()
        {
            GameItemModel[] groundItems = ItemCollection.Values.Where(it => it.OwnerEntity == ItemOwner.Ground).ToArray();

            foreach (GameItemModel item in groundItems)
            {
                uint hash = NAPI.Util.GetHashKey(item.Hash);

                if (Enum.IsDefined(typeof(WeaponHash), hash))
                {
                    // prop hash za oruzje
                    hash = NAPI.Util.GetHashKey(Constants.WeaponItemModels[(WeaponHash)hash]);
                }

                item.ObjectHandle = NAPI.Object.CreateObject(hash, item.Position, new Vector3(), 255, item.Dimension);
            }
        }

        public static GameItemModel GetPlayerItemModelFromHash(int playerId, string hash)
        {
            return ItemCollection.Values.FirstOrDefault(i => i.OwnerEntity == ItemOwner.Player && i.OwnerIdentifier == playerId && i.Hash == hash);
        }

        public static GameItemModel GetClosestItem(Player player, string hash = "")
        {
            return ItemCollection.Values.FirstOrDefault(i => i.OwnerEntity == ItemOwner.Ground && (i.Hash == hash || hash == string.Empty) && player.Position.DistanceTo(i.Position) < 2.0f);
        }

        public static List<GameItemModel> GetClosestGroundItems(Player player)
        {
            return ItemCollection.Values.Where(i => i.OwnerEntity == ItemOwner.Ground && player.Position.DistanceTo(i.Position) < 2.0f).ToList();
        }

        public static GameItemModel GetItemInEntity(int entityId, ItemOwner entity)
        {
            return ItemCollection.Values.FirstOrDefault(i => i.OwnerEntity == entity && i.OwnerIdentifier == entityId);
        }

        public static GameItemModel GetItemModelFromId(int itemId)
        {
            return ItemCollection.ContainsKey(itemId) ? ItemCollection[itemId] : null;
        }

        public static bool HasPlayerItemOnHand(Player player)
        {
            return player.GetSharedData<string>(EntityData.PlayerRightHand) != null || player.CurrentWeapon != WeaponHash.Unarmed;
        }

        public static List<GameItemModel> GetEntityInventory(Entity entity, bool includeWeapons = false)
        {
            int entityId = 0;
            List<GameItemModel> inventory = new List<GameItemModel>();

            List<ItemOwner> owners = new List<ItemOwner> { entity is Player ? ItemOwner.Player : ItemOwner.Vehicle };

            if (includeWeapons && entity is Player)
            {
                owners.Add(ItemOwner.Wheel);
                owners.Add(ItemOwner.RightHand);
            }

            if (entity is Player)
            {
                entityId = entity.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Id;
            }
            else
            {
                entityId = entity.GetData<int>(EntityData.VehicleId);
            }

            GameItemModel[] itemArray = ItemCollection.Values.Where(i => owners.Contains(i.OwnerEntity) && i.OwnerIdentifier == entityId).ToArray();

            foreach (GameItemModel item in itemArray)
            {
                GameItemModel gameItem = new GameItemModel();
                BusinessItemModel businessItem = Business.GetBusinessItemFromHash(item.Hash);
                if (businessItem != null)
                {
                    gameItem.Description = businessItem.Description;
                    gameItem.Type = businessItem.Type;
                }
                else
                {
                    gameItem.Description = item.Hash;
                    gameItem.Type = ItemTypes.Weapon;
                }

                gameItem.Id = item.Id;
                gameItem.Hash = item.Hash;
                gameItem.Amount = item.Amount;

                inventory.Add(gameItem);
            }

            return inventory;
        }

        public static void ConsumeItem(Player player, GameItemModel item, BusinessItemModel businessItem)
        {
            item.Amount--;

            if (businessItem.Health != 0 && player.Health < 100)
            {
                int newHealth = player.Health + businessItem.Health;

                player.Health = newHealth > 100 ? 100 : newHealth;
            }

            if (businessItem.AlcoholLevel > 0)
            {
                PlayerTemporaryModel playerModel = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);
                playerModel.DrunkLevel += businessItem.AlcoholLevel;

                if (playerModel.DrunkLevel > Constants.WASTED_LEVEL)
                {
                    player.SetSharedData(EntityData.PlayerWalkingStyle, "move_m@drunk@verydrunk");
                    NAPI.ClientEvent.TriggerClientEventForAll("changePlayerWalkingStyle", player.Handle, "move_m@drunk@verydrunk");
                }
            }

            if (item.Amount == 0)
            {
                NAPI.ClientEvent.TriggerClientEventInDimension(player.Dimension, "dettachItemFromPlayer", player.Value);
                player.ResetSharedData(EntityData.PlayerRightHand);

                ItemCollection.Remove(item.Id);
                Task.Run(() => DatabaseOperations.DeleteSingleRow("items", "id", item.Id)).ConfigureAwait(false);
            }
            else
            {
                Task.Run(() => DatabaseOperations.UpdateItem(item)).ConfigureAwait(false);
            }

            player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.player_inventory_consume, businessItem.Description.ToLower()));
        }

        public static void DropItem(Player player, GameItemModel itemDropped, BusinessItemModel businessItem, int amount, bool droppedFromHand)
        {
            //Ima li dole itema sa kojim treba da se spoji kad ga bacimo?
            GameItemModel itemGround = GetClosestItem(player, itemDropped.Hash);

            //Oruzje?
            WeaponHash weaponHash = NAPI.Util.WeaponNameToModel(itemDropped.Hash);


            //Smanjiti mu kolicinu, ako se ne baca sve (iz inv se moze baciti samo deo, ovu funkciju ne poziva samo /drop komanda)
            itemDropped.Amount -= itemDropped.Amount >= amount ? amount : itemDropped.Amount;

            if (itemGround != null) //postoji isti item blizu, spajamo ga
            {
                itemGround.Amount += amount;

                // Update the closest item's amount
                Task.Run(() => DatabaseOperations.UpdateItem(itemGround)).ConfigureAwait(false);
            }
            else
            {
                // Get the hash from the item dropped
                uint itemHash = weaponHash != 0 ? NAPI.Util.GetHashKey(Constants.WeaponItemModels[weaponHash]) : NAPI.Util.GetHashKey(itemDropped.Hash);
                itemGround = itemDropped.Copy();//pravimo novi item na zemlji
                itemGround.Amount = amount; //novog itema ima onoliko koliko starog bacamo, ne moramo baciti sve iz inv. zato iznad ide kopija
                itemGround.OwnerEntity = ItemOwner.Ground;
                itemGround.Dimension = player.Dimension;
                itemGround.Position = player.Position.Subtract(new Vector3(0.0f, 0.0f, 0.8f));
                itemGround.ObjectHandle = NAPI.Object.CreateObject(itemHash, itemGround.Position, player.Rotation, 255, itemGround.Dimension);//new Vector3() u player.rot
                Task.Run(() => DatabaseOperations.AddNewItem(itemGround)).ConfigureAwait(false);
            }

            if (itemDropped.Amount == 0) //da li smo bacili sve?
            {
                if (droppedFromHand)
                {
                    player.ResetSharedData(EntityData.PlayerRightHand);

                    if (weaponHash != 0)
                    {
                        player.RemoveWeapon(weaponHash);
                    }
                    else
                    {
                        NAPI.ClientEvent.TriggerClientEventInDimension(player.Dimension, "dettachItemFromPlayer", player.Value);
                    }
                }

                // Bacili smo sve, brisemo stari item iz ruke/inv
                ItemCollection.Remove(itemDropped.Id);
                Task.Run(() => DatabaseOperations.DeleteSingleRow("items", "id", itemDropped.Id)).ConfigureAwait(false);
            }
            else
            {
                Task.Run(() => DatabaseOperations.UpdateItem(itemDropped)).ConfigureAwait(false);
            }

            player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.player_inventory_drop, weaponHash != 0 ? weaponHash.ToString().ToLower() : businessItem.Description.ToLower()));
        }

        public static void StoreItemOnHand(Player player)
        {
            GameItemModel itemStoring = null;

            //da li drzi oruzje?
            if(Enum.IsDefined(typeof(WeaponHash), player.CurrentWeapon) && player.CurrentWeapon != WeaponHash.Unarmed)
            {
                itemStoring = Weapons.GetWeaponItem(player, player.CurrentWeapon);
                WeaponHash weaponHash = NAPI.Util.WeaponNameToModel(itemStoring.Hash);
                player.RemoveWeapon(weaponHash);
            }
            else
            {
                string rightHand = player.GetSharedData<string>(EntityData.PlayerRightHand);
                itemStoring = GetItemModelFromId(NAPI.Util.FromJson<AttachmentModel>(rightHand).itemId);

                NAPI.ClientEvent.TriggerClientEventInDimension(player.Dimension, "dettachItemFromPlayer", player.Value);
            }
                player.ResetSharedData(EntityData.PlayerRightHand);

            //Ima li ga u inv?
            GameItemModel inventoryItem = GetPlayerItemModelFromHash(player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Id, itemStoring.Hash);

            if (inventoryItem == null)//nemamo isti item u inventory
            {
                itemStoring.OwnerEntity = ItemOwner.Player;
                //item odlazi u inventory, nema vise koordinate
                itemStoring.Position = new Vector3(0, 0, 0);

                Task.Run(() => DatabaseOperations.UpdateItem(itemStoring)).ConfigureAwait(false);
            }
            else//u inv postoji isti item koji hocemo da ubacimo. spojicemo ih
            {
                //itemu u inv povecavamo kolicinu za kolicinu itema koji ubacujemo
                inventoryItem.Amount += itemStoring.Amount;

                //Item u ruci necemo ubaciti nego cemo obrisati, a item u inv cemo sacuvati u bazu zbog promenjene kolicine
                Task.Run(() => DatabaseOperations.UpdateItem(inventoryItem)).ConfigureAwait(false);
                ItemCollection.Remove(itemStoring.Id);
                Task.Run(() => DatabaseOperations.DeleteSingleRow("items", "id", itemStoring.Id)).ConfigureAwait(false);
            }
        }

        [RemoteEvent("LoadPlayerInventory")]
        public static void LoadPlayerInventoryRemoteEvent(Player player)
        {
            player.TriggerEvent("ShowPlayerInventory", InventoryTarget.Self, GetEntityInventory(player), GetClosestGroundItems(player));
        }

        [RemoteEvent("DropItem")]
        public static void DropItemRemoteEvent(Player player, int itemId, int amount)
        {
            GameItemModel item = GetItemModelFromId(itemId);
            if (item == null) { player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.item_empty); return; }
            else if (item.Hash.Contains("misc")) { player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.item_not_dropable); return; }
            BusinessItemModel businessItem = Business.GetBusinessItemFromHash(item.Hash);

            if (item != null && businessItem != null)
            {
                DropItem(player, item, businessItem, amount, false);
            }
        }

        [RemoteEvent("processMenuAction")]
        public void ProcessMenuActionRemoteEventAsync(Player player, int itemId, string action)
        {
            GameItemModel item = GetItemModelFromId(itemId);
            if (item == null) { player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.item_empty); return; }
            else if (item.Hash.Contains("misc")) { player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.item_useless); return; }
            BusinessItemModel businessItem = Business.GetBusinessItemFromHash(item.Hash);

            if (action.Equals(ArgRes.consume, StringComparison.InvariantCultureIgnoreCase))
            {
                ConsumeItem(player, item, businessItem);

                return;
            }

            CharacterModel characterModel = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database);

            if (action.Equals(ArgRes.open, StringComparison.InvariantCultureIgnoreCase))
            {
                switch (item.Hash)
                {
                    case Constants.ITEM_HASH_PACK_BEER_AM:
                        GameItemModel itemModel = GetPlayerItemModelFromHash(characterModel.Id, Constants.ITEM_HASH_BOTTLE_BEER_AM);

                        if (itemModel == null)
                        {
                            itemModel = new GameItemModel()
                            {
                                Hash = Constants.ITEM_HASH_BOTTLE_BEER_AM,
                                OwnerEntity = ItemOwner.Player,
                                OwnerIdentifier = characterModel.Id,
                                Amount = Constants.ITEM_OPEN_BEER_AMOUNT,
                                Position = new Vector3(),
                                Dimension = player.Dimension
                            };

                            Task.Run(() => DatabaseOperations.AddNewItem(itemModel)).ConfigureAwait(false);
                        }
                        else
                        {
                            itemModel.Amount += Constants.ITEM_OPEN_BEER_AMOUNT;

                            Task.Run(() => DatabaseOperations.UpdateItem(item)).ConfigureAwait(false);
                        }
                        break;
                }

                SubstractPlayerItems(item);

                player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.player_inventory_open, businessItem.Description.ToLower()));

                return;
            }

            if (action.Equals(ArgRes.equip, StringComparison.InvariantCultureIgnoreCase))
            {
                if (HasPlayerItemOnHand(player))
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.right_hand_occupied);
                    return;
                }

                //uzimamo ime itema
                string hash = item.Hash;
                if (Enum.IsDefined(typeof(WeaponHash), hash)) //hocemo da equipamo oruzje?
                {
                    Enum.TryParse(hash, out WeaponHash weapon);

                    if (player.GetWeaponAmmo(weapon) > 0) //imamo li vec to isto oruzje kod sebe?
                    {
                        item.Amount += player.GetWeaponAmmo(weapon);
                        player.SetWeaponAmmo(weapon, item.Amount); //ako vec ima to oruzje, povecamo mu municiju
                        Inventory.ItemCollection.Remove(item.Id); //staro brisemo, ne treba nam
                        Task.Run(() => DatabaseOperations.DeleteSingleRow("items", "id", item.Id)).ConfigureAwait(false);
                    }
                    else
                    {
                        //Dajemo novo oruzje posto nije nasao isto
                        player.GiveWeapon(weapon, 0);
                        player.SetWeaponAmmo(weapon, item.Amount);
                        item.OwnerEntity = ItemOwner.Wheel;
                        item.OwnerIdentifier = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Id;
                        item.Position = new Vector3(0, 0, 0);
                        Task.Run(() => DatabaseOperations.UpdateItem(item)).ConfigureAwait(false);
                    }
                }
                else if (item.Type == ItemTypes.Equipable  || item.Type == ItemTypes.Consumable) //ovo se mora i u klijentu dodati
                {
                    item.OwnerEntity = ItemOwner.RightHand;
                    UtilityFunctions.AttachItemToPlayer(player, item.Id, NAPI.Util.GetHashKey(item.Hash), "IK_R_Hand", businessItem.Position, businessItem.Rotation, EntityData.PlayerRightHand);
                }
                else player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.item_not_equipable);

                player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.player_inventory_equip, businessItem.Description.ToLower()));

                return;
            }

            if (action.Equals(ArgRes.confiscate, StringComparison.InvariantCultureIgnoreCase))
            {
                Player target = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame).SearchedTarget;

                item.OwnerEntity = ItemOwner.Player;
                item.OwnerIdentifier = characterModel.Id;

                player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.police_retired_items_to, target.Name));
                target.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.police_retired_items_from, player.Name));

                Task.Run(() => DatabaseOperations.UpdateItem(item)).ConfigureAwait(false);

                return;
            }

            if (action.Equals(ArgRes.store, StringComparison.InvariantCultureIgnoreCase))
            {
                Vehicle targetVehicle = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame).OpenedTrunk;

                item.OwnerEntity = ItemOwner.Vehicle;
                item.OwnerIdentifier = targetVehicle.GetData<int>(EntityData.VehicleId);

                player.RemoveWeapon(NAPI.Util.WeaponNameToModel(item.Hash));

                player.SendChatMessage(Constants.COLOR_INFO + InfoRes.trunk_stored_items);

                Task.Run(() => DatabaseOperations.UpdateItem(item)).ConfigureAwait(false);

                return;
            }

            if (action.Equals(ArgRes.withdraw, StringComparison.InvariantCultureIgnoreCase))
            {
                if (HasPlayerItemOnHand(player))
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.right_hand_occupied);
                    return;
                }

                Vehicle sourceVehicle = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame).OpenedTrunk;

                WeaponHash weaponHash = NAPI.Util.WeaponNameToModel(item.Hash);

                if (weaponHash != 0)
                {
                    item.OwnerEntity = ItemOwner.Wheel;
                    player.GiveWeapon(weaponHash, 0);
                    player.SetWeaponAmmo(weaponHash, item.Amount);
                }
                else
                {
                    item.OwnerEntity = ItemOwner.Player;
                    player.GiveWeapon(WeaponHash.Unarmed, 0);
                }

                item.OwnerIdentifier = characterModel.Id;

                Chat.SendMessageToNearbyPlayers(player, InfoRes.trunk_item_withdraw, ChatTypes.Me, 20.0f);
                player.SendChatMessage(Constants.COLOR_INFO + InfoRes.trunk_withdraw_items);

                Task.Run(() => DatabaseOperations.UpdateItem(item)).ConfigureAwait(false);

                return;
            }

            if (action.Equals(ArgRes.sell, StringComparison.InvariantCultureIgnoreCase))
            {
                if (businessItem.Products == 0)
                {
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.item_not_sellable);
                    return;
                }

                int wonAmount = (int)Math.Round(item.Amount * businessItem.Products * Constants.PawnMultiplier);
                AddBlackMoney(characterModel.Id, wonAmount);

                ItemCollection.Remove(item.Id);
                Task.Run(() => DatabaseOperations.DeleteSingleRow("items", "id", item.Id)).ConfigureAwait(false);
                item.Amount = 0;

                player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.player_pawned_items, wonAmount));

                return;
            }
        }

        [RemoteEvent("closeInventory")]
        public static void CloseInventoryRemoteEvent(Player player)
        {
            PlayerTemporaryModel data = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);
            data.OpenedTrunk = null;
            data.SearchedTarget = null;
        }

        private void SubstractPlayerItems(GameItemModel item, int amount = 1)
        {
            item.Amount -= amount;
            if (item.Amount != 0) return;

            Task.Run(() => DatabaseOperations.DeleteSingleRow("items", "id", item.Id)).ConfigureAwait(false);
            ItemCollection.Remove(item.Id);
        }

        private void AddBlackMoney(int playerId, int money)
        {
            GameItemModel blackMoney = GetPlayerItemModelFromHash(playerId, Constants.ITEM_HASH_MONEY);

            if (blackMoney != null)
            {
                blackMoney.Amount += money;

                Task.Run(() => DatabaseOperations.UpdateItem(blackMoney)).ConfigureAwait(false);
            }
            else
            {
                blackMoney = new GameItemModel()
                {
                    Amount = money,
                    Hash = Constants.ITEM_HASH_MONEY,
                    OwnerEntity = ItemOwner.Player,
                    OwnerIdentifier = playerId,
                    Position = new Vector3()
                };

                Task.Run(() => DatabaseOperations.AddNewItem(blackMoney)).ConfigureAwait(false);
            }
        }
    }
}
