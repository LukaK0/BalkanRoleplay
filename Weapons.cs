using GTANetworkAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using character;
using Data;
using Data.Persistent;
using Data.Temporary;
using factions;
using messages.information;
using Utility;
using static Utility.Enumerators;

namespace weapons
{
    public class Weapons : Script
    {
        public static Timer weaponTimer;
        private static Dictionary<int, Timer> VehicleWeaponTimer;
        private static List<WeaponCrateModel> weaponCrateList;

        public Weapons()
        {
            VehicleWeaponTimer = new Dictionary<int, Timer>();
            weaponCrateList = new List<WeaponCrateModel>();
        }

        public static void GivePlayerWeaponItems(Player player)
        {
            int playerId = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Id;

            foreach (GameItemModel item in Inventory.ItemCollection.Values)
            {
                if (!int.TryParse(item.Hash, out int _) && item.OwnerIdentifier == playerId && item.OwnerEntity == ItemOwner.Wheel)
                {
                    WeaponHash weaponHash = NAPI.Util.WeaponNameToModel(item.Hash);
                    player.GiveWeapon(weaponHash, 0);
                    player.SetWeaponAmmo(weaponHash, item.Amount);
                }
            }
        }

        public static void GivePlayerNewWeapon(Player player, WeaponHash weapon, int bullets, bool licensed)
        {
            GameItemModel weaponModel = new GameItemModel
            {
                Hash = weapon.ToString(),
                Amount = bullets,
                OwnerEntity = ItemOwner.Wheel,
                OwnerIdentifier = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Id,
                Position = new Vector3(),
                Dimension = 0
            };

            Task.Run(() => DatabaseOperations.AddNewItem(weaponModel)).ConfigureAwait(false);

            player.SetWeaponAmmo(weapon, bullets);

            if (licensed) Task.Run(() => DatabaseOperations.AddLicensedWeapon(weaponModel.Id, player.Name)).ConfigureAwait(false);
        }

        public static GameItemModel GetWeaponItem(Player player, WeaponHash weaponHash)
        {
            int playerId = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Id;

            return Inventory.ItemCollection.Values.FirstOrDefault(i => i.OwnerEntity == ItemOwner.Wheel && i.OwnerIdentifier == playerId && NAPI.Util.WeaponNameToModel(i.Hash) == weaponHash);
        }

        public static string GetGunAmmunitionType(WeaponHash weapon)
        {
            GunModel gunModel = Constants.GUN_LIST.FirstOrDefault(gun => weapon == gun.Weapon);

            return gunModel?.Ammunition ?? string.Empty;
        }

        public static int GetGunAmmunitionCapacity(WeaponHash weapon)
        {
            GunModel gunModel = Constants.GUN_LIST.FirstOrDefault(gun => weapon == gun.Weapon);

            return gunModel?.Capacity ?? 0;
        }

        public static WeaponTypes GetGunWeaponType(WeaponHash weapon)
        {
            GunModel gunModel = Constants.GUN_LIST.FirstOrDefault(gun => weapon == gun.Weapon);
            return gunModel.WeaponType;
        }

        public static GameItemModel GetEquippedWeaponItemModelByHash(int playerId, WeaponHash weapon)
        {
            return Inventory.ItemCollection.Values.FirstOrDefault(itemModel => itemModel.OwnerIdentifier == playerId && (itemModel.OwnerEntity == ItemOwner.Wheel || itemModel.OwnerEntity == ItemOwner.RightHand) && weapon.ToString() == itemModel.Hash);
        }

        public static WeaponCrateModel GetClosestWeaponCrate(Player player, float distance = 1.5f)
        {
            return weaponCrateList.FirstOrDefault(w => player.Position.DistanceTo(w.Position) < distance && w.CarriedEntity == string.Empty);
        }

        public static WeaponCrateModel GetPlayerCarriedWeaponCrate(int playerId)
        {
            return weaponCrateList.FirstOrDefault(w => w.CarriedEntity == ItemOwner.Player.ToString() && w.CarriedIdentifier == playerId);
        }

        public static List<WeaponHash> GetPlayerWeaponNames(Player player)
        {
            GameItemModel[] weaponModels = (Inventory.ItemCollection.Values.Where(itemModel => itemModel.OwnerIdentifier == player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Id && (itemModel.OwnerEntity == ItemOwner.Wheel || itemModel.OwnerEntity == ItemOwner.RightHand))).ToArray();
            List<WeaponHash> playerWeapons = new List<WeaponHash>();
            foreach (GameItemModel w in weaponModels) playerWeapons.Add(ToWeaponHash(w.Hash));
            return playerWeapons;
        }

        public static bool PlayerHasSameWeaponType(Player player, WeaponHash weaponName)
        {
            WeaponTypes tip = GetGunWeaponType(weaponName);
            List<WeaponHash> playerWeapons = GetPlayerWeaponNames(player);
            foreach (WeaponHash weaponHash in playerWeapons) { if (GetGunWeaponType(weaponHash) == tip) return true; }
            return false;
        }      

        public static WeaponHash ToWeaponHash(string weaponName)
        {
            return (WeaponHash)Enum.Parse(typeof(WeaponHash), weaponName, true);
        }


        public static void OnPlayerDisconnected(Player player)
        {
            WeaponCrateModel weaponCrate = GetPlayerCarriedWeaponCrate(player.Value);

            if (weaponCrate != null)
            {
                weaponCrate.Position = new Vector3(player.Position.X, player.Position.Y, player.Position.X - 1.0f);
                weaponCrate.CarriedEntity = string.Empty;
                weaponCrate.CarriedIdentifier = 0;

                weaponCrate.CrateObject.Position = weaponCrate.Position;
            }
        }

        public static void StoreCrateIntoTrunk(Player player, Vehicle vehicle)
        {
            int vehicleId = vehicle.GetData<int>(EntityData.VehicleId);

            string attachmentJson = player.GetSharedData<string>(EntityData.PlayerWeaponCrate);
            AttachmentModel attachment = NAPI.Util.FromJson<AttachmentModel>(attachmentJson);
            WeaponCrateModel weaponCrate = weaponCrateList[attachment.itemId];

            weaponCrate.CarriedEntity = ItemOwner.Vehicle.ToString();
            weaponCrate.CarriedIdentifier = vehicleId;

            UtilityFunctions.RemoveItemOnHands(player);
            player.StopAnimation();

            if (weaponCrateList.Count(c => c.CarriedEntity == ItemOwner.Vehicle.ToString() && vehicleId == c.CarriedIdentifier) == 1)
            {
                Player driver = vehicle.Occupants.Cast<Player>().ToList().FirstOrDefault(o => o.VehicleSeat == (int)VehicleSeat.Driver);

                if (driver != null && driver.Exists)
                {
                    Checkpoint weaponCheckpoint = NAPI.Checkpoint.CreateCheckpoint(4, Coordinates.CrateDeliver, new Vector3(), 2.5f, new Color(198, 40, 40, 200));
                    player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame).JobCheckPoint = weaponCheckpoint.Value;
                    driver.SendChatMessage(Constants.COLOR_INFO + InfoRes.weapon_position_mark);
                    driver.TriggerEvent("showWeaponCheckpoint", Coordinates.CrateDeliver);
                }
            }
            player.SendChatMessage(Constants.COLOR_INFO + InfoRes.trunk_stored_items);
        }

        public static void PickUpCrate(Player player, WeaponCrateModel weaponCrate)
        {
            int index = weaponCrateList.IndexOf(weaponCrate);
            weaponCrate.CarriedEntity = ItemOwner.Player.ToString();
            weaponCrate.CarriedIdentifier = player.Value;
            player.PlayAnimation("anim@heists@box_carry@", "idle", (int)(AnimationFlags.Loop | AnimationFlags.OnlyAnimateUpperBody | AnimationFlags.AllowPlayerControl));

            UtilityFunctions.AttachItemToPlayer(player, index, weaponCrate.CrateObject.Model, "IK_R_Hand", new Vector3(0.0f, -0.5f, -0.25f), new Vector3(), EntityData.PlayerWeaponCrate);

            weaponCrate.CrateObject.Delete();
            weaponCrate.CrateObject = null;
        }

        public static void OnPlayerEnterVehicle(Player player, Vehicle vehicle, int vehicleId)
        {
            if (GetVehicleWeaponCrates(vehicleId) == 0 || vehicle.HasData(EntityData.VehicleWeaponUnpacking)) return;

            Checkpoint weaponCheckpoint = NAPI.Checkpoint.CreateCheckpoint(4, Coordinates.CrateDeliver, new Vector3(), 2.5f, new Color(198, 40, 40, 200));
            player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame).JobColShape = weaponCheckpoint;
            player.SendChatMessage(Constants.COLOR_INFO + InfoRes.weapon_position_mark);
            player.TriggerEvent("showWeaponCheckpoint", Coordinates.CrateDeliver);
        }

        private static List<Vector3> GetRandomWeaponSpawns(int spawnPosition)
        {
            Random random = new Random();
            List<Vector3> weaponSpawns = new List<Vector3>();
            CrateSpawnModel[] cratesInSpawn = GetSpawnsInPosition(spawnPosition);

            while (weaponSpawns.Count < Constants.MAX_CRATES_SPAWN)
            {
                Vector3 crateSpawn = cratesInSpawn[random.Next(cratesInSpawn.Length)].Position;
                if (!weaponSpawns.Contains(crateSpawn))
                {
                    weaponSpawns.Add(crateSpawn);
                }
            }

            return weaponSpawns;
        }

        private static CrateSpawnModel[] GetSpawnsInPosition(int spawnPosition)
        {
            return Constants.CrateSpawnCollection.Where(c => c.SpawnPoint == spawnPosition).ToArray();
        }

        private static CrateContentModel GetRandomCrateContent(int type, int chance)
        {
            WeaponChanceModel weaponAmmo = Constants.WeaponChanceArray.First(w => w.Type == type && w.MinChance <= chance && w.MaxChance >= chance);

            CrateContentModel crateContent = new CrateContentModel();
            {
                crateContent.item = weaponAmmo.Hash;
                crateContent.amount = weaponAmmo.Amount;
            }

            return crateContent;
        }

        private static void OnWeaponPrewarn(object unused)
        {
            weaponTimer.Dispose();

            int currentSpawn = 0;
            weaponCrateList = new List<WeaponCrateModel>();

            Random random = new Random();
            int spawnPosition = random.Next(Constants.MAX_WEAPON_SPAWNS);

            List<Vector3> weaponSpawns = GetRandomWeaponSpawns(spawnPosition);

            foreach (Vector3 spawn in weaponSpawns)
            {
                int type = currentSpawn % 2;
                int chance = random.Next(type == 0 ? Constants.MAX_WEAPON_CHANCE : Constants.MAX_AMMO_CHANCE);
                CrateContentModel crateContent = GetRandomCrateContent(type, chance);

                NAPI.Task.Run(() =>
                {
                    WeaponCrateModel weaponCrate = new WeaponCrateModel();
                    {
                        weaponCrate.ContentItem = crateContent.item;
                        weaponCrate.ContentAmount = crateContent.amount;
                        weaponCrate.Position = spawn;
                        weaponCrate.CarriedEntity = string.Empty;
                        weaponCrate.CrateObject = NAPI.Object.CreateObject(481432069, spawn, new Vector3(), 255, 0);
                    }

                    weaponCrateList.Add(weaponCrate);
                    currentSpawn++;
                });
            }

            foreach (Player player in NAPI.Pools.GetAllPlayers())
            {
                if (Character.IsPlaying(player) && (int)player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Faction > Constants.LAST_STATE_FACTION)
                {
                    player.SendChatMessage(Constants.COLOR_INFO + InfoRes.weapon_spawn_island);
                }
            }

            weaponTimer = new Timer(OnPoliceCalled, null, 240000, Timeout.Infinite);
        }

        private static void OnPoliceCalled(object unused)
        {
            weaponTimer.Dispose();

            foreach (Player player in NAPI.Pools.GetAllPlayers())
            {
                if (Faction.IsPoliceMember(player))
                {
                    player.SendChatMessage(Constants.COLOR_INFO + InfoRes.weapon_spawn_island);
                }
            }

            weaponTimer = new Timer(OnWeaponEventFinished, null, 3600000, Timeout.Infinite);
        }

        private static void OnVehicleUnpackWeapons(object vehicleObject)
        {
            Vehicle vehicle = (Vehicle)vehicleObject;
            int vehicleId = vehicle.GetData<int>(EntityData.VehicleId);

            foreach (WeaponCrateModel weaponCrate in weaponCrateList)
            {
                if (weaponCrate.CarriedEntity != ItemOwner.Vehicle.ToString() || weaponCrate.CarriedIdentifier != vehicleId) continue;

                GameItemModel item = new GameItemModel()
                {
                    Hash = weaponCrate.ContentItem,
                    Amount = weaponCrate.ContentAmount,
                    OwnerEntity = ItemOwner.Vehicle,
                    OwnerIdentifier = vehicleId
                };

                weaponCrate.CarriedIdentifier = 0;
                weaponCrate.CarriedEntity = string.Empty;

                Task.Run(() => DatabaseOperations.AddNewItem(item)).ConfigureAwait(false);
            }

            Player driver = NAPI.Pools.GetAllPlayers().FirstOrDefault(p => p.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame).LastVehicle == vehicle);

            if (driver != null && driver.Exists)
            {
                driver.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame).LastVehicle = null;
                driver.SendChatMessage(Constants.COLOR_INFO + InfoRes.weapons_unpacked);
            }

            VehicleWeaponTimer[vehicleId].Dispose();
            VehicleWeaponTimer.Remove(vehicleId);
            vehicle.ResetData(EntityData.VehicleWeaponUnpacking);
        }

        private static void OnWeaponEventFinished(object unused)
        {
            weaponTimer.Dispose();

            foreach (WeaponCrateModel crate in weaponCrateList)
            {
                if (crate.CrateObject != null && crate.CrateObject.Exists) NAPI.Task.Run(() => crate.CrateObject.Delete());
            }

            weaponCrateList = new List<WeaponCrateModel>();
            weaponTimer = null;
        }

        private static int GetVehicleWeaponCrates(int vehicleId)
        {
            return weaponCrateList.Count(w => w.CarriedEntity == ItemOwner.Vehicle.ToString() && w.CarriedIdentifier == vehicleId);
        }

        [ServerEvent(Event.PlayerExitVehicle)]
        public void OnPlayerExitVehicle(Player player, Vehicle vehicle)
        {
            if (vehicle != null && vehicle.HasData(EntityData.VehicleId))
            {
                int vehicleId = vehicle.GetData<int>(EntityData.VehicleId);

                PlayerTemporaryModel data = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

                if (data.JobColShape != null && data.JobColShape.Exists && GetVehicleWeaponCrates(vehicleId) > 0)
                {
                    player.TriggerEvent("deleteWeaponCheckpoint");
                }
            }
        }

        [ServerEvent(Event.PlayerEnterCheckpoint)]
        public void OnPlayerEnterCheckpoint(Checkpoint checkpoint, Player player)
        {
            PlayerTemporaryModel data = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

            if (data.JobColShape == null || data.JobColShape != checkpoint || player.VehicleSeat != (int)VehicleSeat.Driver) return;

            Vehicle vehicle = player.Vehicle;
            int vehicleId = vehicle.GetData<int>(EntityData.VehicleId);

            if (GetVehicleWeaponCrates(vehicleId) > 0)
            {
                data.JobColShape.Delete();
                data.JobColShape = null;

                player.TriggerEvent("deleteWeaponCheckpoint");

                vehicle.EngineStatus = false;
                data.LastVehicle = vehicle;
                vehicle.SetData(EntityData.VehicleWeaponUnpacking, true);

                VehicleWeaponTimer.Add(vehicleId, new Timer(OnVehicleUnpackWeapons, vehicle, 60000, Timeout.Infinite));

                player.SendChatMessage(Constants.COLOR_INFO + InfoRes.wait_for_weapons);
            }
        }

        [RemoteEvent("reloadPlayerWeapon")]
        public void ReloadPlayerWeaponEvent(Player player, int currentBullets)
        {
            WeaponHash weapon = player.CurrentWeapon;
            int maxCapacity = GetGunAmmunitionCapacity(weapon);

            if (currentBullets < maxCapacity)
            {
                string bulletType = GetGunAmmunitionType(weapon);
                int playerId = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Id;
                GameItemModel bulletItem = Inventory.GetPlayerItemModelFromHash(playerId, bulletType);

                if (bulletItem != null)
                {
                    int bulletsLeft = maxCapacity - currentBullets;
                    if (bulletsLeft >= bulletItem.Amount)
                    {
                        currentBullets += bulletItem.Amount;

                        Task.Run(() => DatabaseOperations.DeleteSingleRow("items", "id", bulletItem.Id)).ConfigureAwait(false);
                        Inventory.ItemCollection.Remove(bulletItem.Id);
                    }
                    else
                    {
                        currentBullets += bulletsLeft;
                        bulletItem.Amount -= bulletsLeft;

                        Task.Run(() => DatabaseOperations.UpdateItem(bulletItem)).ConfigureAwait(false);
                    }

                    GameItemModel weaponItem = GetEquippedWeaponItemModelByHash(playerId, weapon);
                    weaponItem.Amount = currentBullets;

                    Task.Run(() => DatabaseOperations.UpdateItem(weaponItem)).ConfigureAwait(false);

                    player.SetWeaponAmmo(weapon, currentBullets);
                    player.TriggerEvent("makePlayerReload");
                }
            }
        }

        [RemoteEvent("updateWeaponBullets")]
        public void UpdateWeaponBullets(Player player, int bullets)
        {
            if (player.CurrentWeapon != WeaponHash.Unarmed)
            {
                GameItemModel item = GetEquippedWeaponItemModelByHash(player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Id, player.CurrentWeapon);

                item.Amount = bullets;

                Task.Run(() => DatabaseOperations.UpdateItem(item)).ConfigureAwait(false);
            }
        }
    }
}
