 using GTANetworkAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Schema;
 using RAGE;
 using Buildings;
using Buildings.Businesses;
using Buildings.Houses;
using Data;
 using Data.Extended;
 using Data.Persistent;
using jobs;
using messages.administration;
using messages.arguments;
using messages.error;
using messages.general;
using messages.help;
using Utility;
using vehicles;
using static Utility.Enumerators;

namespace Administration
{
    public static class Admin
    {
        public static List<PermissionModel> PermissionList;
        public static Dictionary<int, string> AdminTicketCollection = new Dictionary<int, string>();

        public static bool HasUserCommandPermission(Player player, string command, string option = "")
        {
            int playerId = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Id;

            return PermissionList.Any(p => p.PlayerId == playerId && command == p.Command && option == string.Empty || option == p.Option);
        }

        public static void ShowVehicleInfo(Player player)
        {
            Vehicle vehicle = Vehicles.GetClosestVehicle(player);

            if (vehicle == null || !vehicle.Exists)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_vehicles_near);
                return;
            }

            VehicleModel vehModel = Vehicles.GetVehicleById<VehicleModel>(vehicle.GetData<int>(EntityData.VehicleId));

            player.SendChatMessage(Constants.COLOR_HELP + string.Format(GenRes.vehicle_check_title, vehModel.Id));
            player.SendChatMessage(Constants.COLOR_HELP + GenRes.vehicle_model + (VehicleHash)vehModel.Model);
            player.SendChatMessage(Constants.COLOR_HELP + GenRes.owner + vehModel.Owner);
            if(vehModel.Faction<100)player.SendChatMessage(Constants.COLOR_HELP + GenRes.faction + (PlayerFactions)vehModel.Faction);
            else player.SendChatMessage(Constants.COLOR_HELP + GenRes.faction + (PlayerJobs)(vehModel.Faction-100));


        }

        //Za /veh
        public static void CreateAdminVehicle1(Player player, string arguments)
        {
            if (arguments.Length == 0)
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.veh);
                return;
            }

            VehicleModel vehicle = new VehicleModel()
            {
                Model = NAPI.Util.GetHashKey(arguments),
                Faction = (int)PlayerFactions.Admin,
                Position = UtilityFunctions.GetForwardPosition(player, 2.5f),
                Heading = player.Rotation.Z,
                Dimension = player.Dimension,
                ColorType = Constants.VEHICLE_COLOR_TYPE_CUSTOM,
                FirstColor = "0,0,0",
                SecondColor = "0,0,0",
                Pearlescent = 0,
                Owner = string.Empty,
                Plate = string.Empty,
                Price = 0,
                Parking = 0,
                Parked = 0,
                Gas = 50.0f,
                Kms = 0.0f
            };
            _ = Vehicles.CreateVehicle(player, vehicle, true);
        }

        //ostali custom
        public static void CreateAdminVehicle(Player player, string[] arguments)
        {
            if (arguments.Length != 4 || !UtilityFunctions.CheckColorStructure(arguments[2]) || !UtilityFunctions.CheckColorStructure(arguments[3]))
            {
                player.SendChatMessage(Constants.COLOR_HELP + HelpRes.vehicle_create);
                return;
            }

            VehicleModel vehicle = new VehicleModel()
            {
                Model = NAPI.Util.GetHashKey(arguments[1]),
                Faction = (int)PlayerFactions.Admin,
                Position = UtilityFunctions.GetForwardPosition(player, 2.5f),
                Heading = player.Rotation.Z,
                Dimension = player.Dimension,
                ColorType = Constants.VEHICLE_COLOR_TYPE_CUSTOM,
                FirstColor = arguments[2],
                SecondColor = arguments[3],
                Pearlescent = 0,
                Owner = string.Empty,
                Plate = string.Empty,
                Price = 0,
                Parking = 0,
                Parked = 0,
                Gas = 50.0f,
                Kms = 0.0f
            };

            _ = Vehicles.CreateVehicle(player, vehicle, true);
        }

        public static void ModifyVehicleColor(Player player,int vehicleId ,string firstColor, string secondColor)
        {
            Vehicle vehicle = Vehicles.GetVehicleById<Vehicle>(vehicleId);

            if (vehicle == null || !vehicle.Exists)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_vehicles_near);
                return;
            }

            VehicleModel vehModel = Vehicles.GetVehicleById<VehicleModel>(vehicle.GetData<int>(EntityData.VehicleId));
            vehModel.ColorType = Constants.VEHICLE_COLOR_TYPE_CUSTOM;
            vehModel.FirstColor = firstColor;
            vehModel.SecondColor = secondColor;
            vehModel.Pearlescent = 0;

            Mechanic.RepaintVehicle(vehicle, vehModel);

            Dictionary<string, object> keyValues = new Dictionary<string, object>()
            {
                { "colorType", vehModel.ColorType },
                { "firstColor", vehModel.FirstColor },
                { "secondColor", vehModel.SecondColor },
                { "pearlescent", vehModel.Pearlescent }
            };

            Task.Run(() => DatabaseOperations.UpdateVehicleValues(keyValues, vehModel.Id)).ConfigureAwait(false);
        }

        public static void ChangeVehicleDimension(Player player, int vehicleId, uint dimension)
        {
            Vehicle veh = Vehicles.GetVehicleById<Vehicle>(vehicleId);

            if (veh == null || !veh.Exists)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_not_exists);
                return;
            }

            veh.Dimension = dimension;
            Vehicles.GetVehicleById<VehicleModel>(veh.GetData<int>(EntityData.VehicleId)).Dimension = dimension;

            Dictionary<string, object> keyValues = new Dictionary<string, object>() { { "dimension", dimension } };
            Task.Run(() => DatabaseOperations.UpdateVehicleValues(keyValues, vehicleId)).ConfigureAwait(false);

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.vehicle_dimension_modified, dimension));
        }

        public static void ChangeVehicleFaction(Player player, int vehicleId,int faction)
        {
            Vehicle veh = Vehicles.GetVehicleById<Vehicle>(vehicleId);

            if (veh == null || !veh.Exists)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_vehicles_near);
                return;
            }

            Vehicles.GetVehicleById<VehicleModel>(veh.GetData<int>(EntityData.VehicleId)).Faction = faction;

            Dictionary<string, object> keyValues = new Dictionary<string, object>() { { "faction", faction } };
            Task.Run(() => DatabaseOperations.UpdateVehicleValues(keyValues, veh.GetData<int>(EntityData.VehicleId))).ConfigureAwait(false);

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.vehicle_faction_modified, faction));
        }

        public static void UpdateVehicleSpawn(Player player)
        {
            VehicleModel vehicle = Vehicles.GetVehicleById<VehicleModel>(player.Vehicle.GetData<int>(EntityData.VehicleId));

            vehicle.Position = player.Vehicle.Position;
            vehicle.Heading = player.Vehicle.Heading;

            Dictionary<string, object> keyValues = new Dictionary<string, object>()
            {
                { "posX", vehicle.Position.X },
                { "posY", vehicle.Position.Y },
                { "posZ", vehicle.Position.Z },
                { "rotation", vehicle.Heading }
            };

            Task.Run(() => DatabaseOperations.UpdateVehicleValues(keyValues, vehicle.Id)).ConfigureAwait(false);

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + AdminRes.vehicle_pos_updated);
        }

        public static void ChangeVehicleOwner(Player player, int vehicleId, string owner)
        {
            Vehicle veh = Vehicles.GetVehicleById<Vehicle>(vehicleId);

            if (veh == null || !veh.Exists)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.no_vehicles_near);
                return;
            }

            Vehicles.GetVehicleById<VehicleModel>(veh.GetData<int>(EntityData.VehicleId)).Owner = owner;
            Vehicles.GetVehicleById<VehicleModel>(veh.GetData<int>(EntityData.VehicleId)).Faction = 0;

            Dictionary<string, object> keyValues = new Dictionary<string, object>() { { "owner", owner }, { "faction", 0 }};
            Task.Run(() => DatabaseOperations.UpdateVehicleValues(keyValues, veh.GetData<int>(EntityData.VehicleId))).ConfigureAwait(false);

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.vehicle_owner_modified, owner));
        }

        public static void RemoveVehicle(Player player, int vehicleId)
        {
            Vehicle veh = Vehicles.GetVehicleById<Vehicle>(vehicleId);

            if (veh == null || !veh.Exists)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_not_exists);
                return;
            }

            veh.Delete();
            Vehicles.IngameVehicles.Remove(vehicleId);
            Task.Run(() => DatabaseOperations.DeleteSingleRow("vehicles", "id", vehicleId)).ConfigureAwait(false);
        }

        public static void BringVehicle(Player player, int vehicleId)
        {
            Vehicle veh = Vehicles.GetVehicleById<Vehicle>(vehicleId);

            if (veh == null || !veh.Exists)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_not_exists);
                return;
            }

            veh.Position = UtilityFunctions.GetForwardPosition(player, 2.5f);
            Vehicles.GetVehicleById<VehicleModel>(veh.GetData<int>(EntityData.VehicleId)).Position = veh.Position;

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.vehicle_bring, vehicleId));
        }

        public static void MovePlayerToVehicle(Player player, int vehicleId)
        {
            VehicleModel veh = Vehicles.GetVehicleById<VehicleModel>(vehicleId);

            if (veh == null)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_not_exists);
                return;
            }

            Vector3 position = veh.Parking == 0 ? UtilityFunctions.GetForwardPosition(Vehicles.GetVehicleById<Vehicle>(vehicleId), 2.5f) : Parking.GetParkingById(veh.Parking).Position;

            BuildingHandler.RemovePlayerFromBuilding(player, position, 0);

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.vehicle_goto, vehicleId));
        }

        public static void ChangeVehicleGas(Player player, int vehicleId, float gas)
        {
            if (gas < 0 || gas > 50)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_gas_incorrect);
                return;
            }

            VehicleModel veh = Vehicles.GetVehicleById<VehicleModel>(vehicleId);

            if (veh == null)
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_not_exists);
                return;
            }

            veh.Gas = gas;

            if (veh.Parked == 0)
            {
                Player driver = Vehicles.GetVehicleById<Vehicle>(vehicleId).Occupants.Cast<Player>().ToList().FirstOrDefault(o => o.VehicleSeat == (int)VehicleSeat.Driver);

                if (driver != null && driver.Exists)
                {
                    driver.TriggerEvent("UpdateVehicleGas", gas);
                }
            }

            Dictionary<string, object> keyValues = new Dictionary<string, object>() { { "gas", gas } };
            Task.Run(() => DatabaseOperations.UpdateVehicleValues(keyValues, vehicleId)).ConfigureAwait(false);

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.vehicle_gas, gas));
        }

        public static void ShowBusinessInfo(Player player, BusinessModel business)
        {
            player.SendChatMessage(string.Format(GenRes.business_check_title, business.Id));
            player.SendChatMessage(GenRes.name + business.Caption);
            player.SendChatMessage(GenRes.ipl + business.Ipl);
            player.SendChatMessage(GenRes.owner + business.Owner);
            player.SendChatMessage(GenRes.products + business.Products);
            player.SendChatMessage(GenRes.multiplier + business.Multiplier);
        }

        public static async Task CreateBusinessAsync(Player player, int type, string place)
        {
            BusinessModel business = new BusinessModel()
            {
                Caption = GenRes.business,
                Ipl = BuildingHandler.GetBusinessIpl(type),
                Entrance = player.Position,
                Dimension = place == ArgRes.inner ? player.Dimension : 0,
                Multiplier = 3.0f,
                Owner = string.Empty,
                Locked = false
            };

            if (place.Equals(ArgRes.outer, StringComparison.InvariantCultureIgnoreCase))
            {
                business.Ipl.Name = string.Empty;
            }

            business.Id = await DatabaseOperations.AddNewBusiness(business).ConfigureAwait(false);

            ColShape colShape = NAPI.ColShape.CreateCylinderColShape(business.Entrance, 2.5f, 1.0f, business.Dimension);

            Entities.Colshapes.CreateEntity = NetHandle => Business.GenerateBusinessElements(colShape.Handle, business);

            Business.BusinessCollection.Add(business.Id, business);
        }

        public static void UpdateBusinessName(Player player, BusinessModel business, string businessName)
        {
            business.Caption = businessName;

            NAPI.TextLabel.SetTextLabelText(business.Label, businessName);

            Dictionary<int, BusinessModel> businessCollection = new Dictionary<int, BusinessModel>();
            businessCollection.Add(business.Id, business);
            Task.Run(() => DatabaseOperations.UpdateBusinesses(businessCollection)).ConfigureAwait(false);
            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.business_name_modified, businessName));
        }

        public static void UpdateBusinessType(Player player, BusinessModel business, BusinessTypes businessType)
        {
            business.Ipl = BuildingHandler.GetBusinessIpl((int)businessType);

            Dictionary<int, BusinessModel> businessCollection = new Dictionary<int, BusinessModel>() { { business.Id, business } };
            Task.Run(() => DatabaseOperations.UpdateBusinesses(businessCollection)).ConfigureAwait(false);

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.business_type_modified, businessType));
        }

        public static void SendHouseInfo(Player player, HouseModel house)
        {
            player.SendChatMessage(string.Format(GenRes.house_check_title, house.Id));
            player.SendChatMessage(GenRes.name + house.Caption);
            player.SendChatMessage(GenRes.ipl + house.Ipl);
            player.SendChatMessage(GenRes.owner + house.Owner);
            player.SendChatMessage(GenRes.price + house.Price);
            player.SendChatMessage(GenRes.status + house.State);
        }

        public static async Task CreateHouseAsync(Player player, int type)
        {
            HouseModel house = new HouseModel()
            {
                Ipl = BuildingHandler.GetHouseIpl(type),
                Caption = GenRes.house,
                Entrance = player.Position,
                Dimension = player.Dimension,
                Price = 10000,
                Owner = string.Empty,
                State = HouseState.Buyable,
                Tenants = 2,
                Rental = 0,
                Locked = true
            };

            house.Id = await DatabaseOperations.AddHouse(house).ConfigureAwait(false);

            house.Label = NAPI.TextLabel.CreateTextLabel(House.GetHouseLabelText(house), house.Entrance, 20.0f, 0.75f, 4, new Color(255, 255, 255));

            Entities.Colshapes.CreateEntity = NetHandle => house.ColShape = new ColShapeModel(NAPI.ColShape.CreateCylinderColShape(house.Entrance, 2.5f, 1.0f).Handle);
            house.ColShape.LinkedId = house.Id;
            house.ColShape.LinkedType = ColShapeTypes.HouseEntrance;
            house.ColShape.InstructionalButton = HelpRes.enter_house;

            House.HouseCollection.Add(house.Id, house);

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + AdminRes.house_created);
        }

        public static void UpdateHousePrice(Player player, HouseModel house, int price)
        {
            house.Price = price;
            house.State = HouseState.Buyable;
            house.Label.Text = House.GetHouseLabelText(house);

            Task.Run(() => DatabaseOperations.UpdateHouse(house)).ConfigureAwait(false);

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.house_price_modified, price));
        }

        public static void UpdateHouseState(Player player, HouseModel house, HouseState state)
        {
            house.State = state;
            house.Label.Text = House.GetHouseLabelText(house);

            Task.Run(() => DatabaseOperations.UpdateHouse(house)).ConfigureAwait(false);

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.house_status_modified, state));
        }

        public static async Task CreateInteriorAsync(Player player, int type)
        {
            InteriorModel interior = new InteriorModel()
            {
                Caption = GenRes.interior,
                Entrance = player.Position,
                Dimension = player.Dimension,
                Label = NAPI.TextLabel.CreateTextLabel(GenRes.interior, player.Position, 30.0f, 0.75f, 4, new Color(255, 255, 255)),
                Ipl = BuildingHandler.GetInteriorIpl(type)
            };

            interior.Id = await DatabaseOperations.AddInteriorAsync(interior);
            GenericInterior.InteriorCollection.Add(interior.Id, interior);

            Entities.Colshapes.CreateEntity = NetHandle => interior.ColShape = new ColShapeModel(NAPI.ColShape.CreateCylinderColShape(player.Position, 2.5f, 1.0f, player.Dimension).Handle);
            interior.ColShape.LinkedId = interior.Id;
            interior.ColShape.LinkedType = ColShapeTypes.InteriorEntrance;
            interior.ColShape.InstructionalButton = HelpRes.enter_building;
        }

        public static void ChangeInteriorType(Player player, InteriorModel interior, InteriorTypes type)
        {
            interior.Ipl = BuildingHandler.GetInteriorIpl((int)type);

            Task.Run(() => DatabaseOperations.UpdateInterior(interior)).ConfigureAwait(false);

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.interior_type_modified, type));
        }

        public static void ChangeInteriorBlip(Player player, InteriorModel interior, int blipId)
        {
            interior.BlipSprite = blipId;

            if (blipId == 0 && interior.Icon != null && interior.Icon.Exists)
            {
                interior.Icon.Delete();
            }
            else if (interior.Icon != null && interior.Icon.Exists)
            {
                interior.Icon.Sprite = (uint)blipId;
            }
            else
            {
                interior.Icon = NAPI.Blip.CreateBlip(interior.BlipSprite, interior.Entrance, 1.0f, 0, interior.Caption);
                interior.Icon.ShortRange = true;
            }

            Task.Run(() => DatabaseOperations.UpdateInterior(interior)).ConfigureAwait(false);

            player.SendChatMessage(Constants.COLOR_ADMIN_INFO + string.Format(AdminRes.interior_blip_modified, interior.BlipSprite));
        }

        public static void Debug(string poruka, string color="")
        {
            /*
            Prihvata boje:
            Red Green Blue Cyan Magenta Yellow Black White Gray DarkRed DarkGreen DarkBlue DarkCyan DarkMagenta DarkYellow DarkGray
            Ako mu se ne prosledi boja ili mu se prosledi pogresna, stampa crveno
            */
            if (!Enum.TryParse(color, true, out ConsoleColor boja)) boja = ConsoleColor.Red;
            Console.ForegroundColor = boja;
            Console.WriteLine(poruka);
            Console.ResetColor();
        }
    }
}
