using RAGE;
using RAGE.Elements;
using Utility;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Ui;
using Jobs;
using System.Linq;

namespace Vehicles
{
    class VehicleHandler : Events.Script
    {
        public static Vector3 lastPosition = null;
        public static Vehicle lastVehicle = null;

        private Blip vehicleLocationBlip = null;
        private Checkpoint vehicleLocationCheckpoint = null;

        private static bool seatbelt;
        private static float kms = 0.0f;
        private static float gas = 0.0f;
        private static float distance = 0.0f;
        private static float consumed = 0.0f;

        private static int LastChecked;

        public VehicleHandler()
        {
            Events.Add("initializeSpeedometer", InitializeSpeedometerEvent);
			Events.Add("UpdateVehicleGas", UpdateVehicleGasEvent);
            Events.Add("removeSpeedometer", RemoveSpeedometerEvent);
            Events.Add("locateVehicle", LocateVehicleEvent);
            Events.Add("toggleVehicleDoor", ToggleVehicleDoorEvent);
            Events.Add("toggleSeatbelt", ToggleSeatbeltEvent);
            Events.Add("KeepVehicleEngineState", KeepVehicleEngineStateEvent);

            Events.OnPlayerLeaveVehicle += PlayerLeaveVehicleEvent;
            Events.OnPlayerEnterCheckpoint += OnPlayerEnterCheckpoint;
            Events.OnEntityStreamIn += EntityStreamInEvent;

            Player.LocalPlayer.SetConfigFlag(32, !seatbelt);
        }

        public static void UpdateSpeedometer()
        {
            int currentTime = RAGE.Game.Misc.GetGameTimer();

            lastVehicle = Player.LocalPlayer.Vehicle;
            Vector3 currentPosition = lastVehicle.Position;

            // speedo
            Vector3 velocity = lastVehicle.GetVelocity();
            int health = lastVehicle.GetHealth();
            int maxHealth = lastVehicle.GetMaxHealth();

            int healthPercent = (int)Math.Round((decimal)(health  * 100) / maxHealth);
            int speed = (int)Math.Round(Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y + velocity.Z * velocity.Z) * 3.6f);

            // kilometraza i potrosnja
            distance = Vector3.Distance(currentPosition, lastPosition);
            consumed = distance * Constants.CONSUME_PER_METER;
            lastPosition = currentPosition;

            if (gas - consumed <= 0.0f || lastVehicle.GetHealth() == 0)
            {
                Events.CallRemote("stopPlayerCar");
                consumed = 0.0f;
            }

            kms += distance;
            gas -= consumed;

            distance = 0.0f;
            consumed = 0.0f;

            if (currentTime - LastChecked > 75)
            {
                LastChecked = currentTime;

                double vehicleKms = Math.Round(kms, 1);

                BrowserManager.Browser.Call("updateSpeedometer", speed, Math.Round(gas, 1), vehicleKms, Taxi.Destiny != null);

                if (Taxi.Destiny == null) return;

                Entities.Players.Streamed.FindAll(p => p != Player.LocalPlayer && p.Vehicle == lastVehicle).ForEach(p =>
                {
                    p.Call("UpdateTaxiMeter", vehicleKms);
                });
            }
        }

        private void InitializeSpeedometerEvent(object[] args)
        {
            kms = (float)Convert.ToDouble(args[0]);
            gas = (float)Convert.ToDouble(args[1]);

            distance = 0.0f;
            consumed = 0.0f;
            lastPosition = Player.LocalPlayer.Vehicle.Position;

            string vehicleName = RAGE.Game.Vehicle.GetDisplayNameFromVehicleModel(Player.LocalPlayer.Vehicle.Model);

            BrowserManager.Browser.Call("showSpeedometer", vehicleName, 0, Math.Round(gas, 1), Math.Round(kms, 1));
        }

        private void UpdateVehicleGasEvent(object[] args)
        {
            gas = (float)Convert.ToDouble(args[0]);
		}		

        public static void RemoveSpeedometerEvent(object[] args)
        {
            if (seatbelt)
            {
                seatbelt = false;
                Events.CallRemote("toggleSeatbelt", seatbelt);
            }

            lastPosition = null;

            if (lastVehicle != null && lastVehicle.Exists)
            {
                Events.CallRemote("saveVehicleConsumes", lastVehicle.RemoteId, lastVehicle.IsInWater(), kms, gas);
            }

            lastVehicle = null;

            BrowserManager.Browser.Call("hideSpeedometer");
        }

        private void LocateVehicleEvent(object[] args)
        {
            Vector3 position = (Vector3)args[0];

            vehicleLocationBlip = new Blip(1, position, string.Empty, 1, 1);
            vehicleLocationCheckpoint = new Checkpoint(4, position, 2.5f, new Vector3(), new RGBA(198, 40, 40, 200));
        }

        private void ToggleVehicleDoorEvent(object[] args)
        {
            int vehicleId = Convert.ToInt32(args[0]);
            int door = Convert.ToInt32(args[1]);
            bool opened = Convert.ToBoolean(args[2]);

            Vehicle vehicle = Entities.Vehicles.GetAtRemote((ushort)vehicleId);

            if (opened)
            {
                vehicle.SetDoorOpen(door, false, false);
            }
            else
            {
                vehicle.SetDoorShut(door, true);
            }
        }

        private void ToggleSeatbeltEvent(object[] args)
        {
            seatbelt = !seatbelt;
            Player.LocalPlayer.SetConfigFlag(32, !seatbelt);

            BrowserManager.Browser.Call("toggleVehicleWarning", "seatbelt");

            Events.CallRemote("toggleSeatbelt", seatbelt);
        }

        private void KeepVehicleEngineStateEvent(object[] args)
        {
            ushort vehicleId = (ushort)Convert.ToInt32(args[0]);
            bool state = Convert.ToBoolean(args[1]);

            Vehicle vehicle = Entities.Vehicles.GetAtRemote(vehicleId);
            vehicle.SetEngineOn(state, true, true);
            vehicle.SetJetEngineOn(state);
        }

        private void PlayerLeaveVehicleEvent(Vehicle vehicle, int seatId)
        {
            if (lastPosition != null)
            {
                RemoveSpeedometerEvent(null);
            }
        }

        private void OnPlayerEnterCheckpoint(Checkpoint checkpoint, Events.CancelEventArgs cancel)
        {
            if (vehicleLocationCheckpoint == null || checkpoint.Id != vehicleLocationCheckpoint.Id) return;

            cancel.Cancel = true;

            vehicleLocationCheckpoint.Destroy();
            vehicleLocationCheckpoint = null;

            vehicleLocationBlip.Destroy();
            vehicleLocationBlip = null;
        }

        private void EntityStreamInEvent(Entity entity)
        {
            if (entity == null || entity.IsNull || entity.IsLocal || entity.Type != RAGE.Elements.Type.Vehicle) return;

            Vehicle vehicle = (Vehicle)entity;
            object doorState = entity.GetSharedData(Constants.VEHICLE_DOORS_STATE);

            if (doorState == null) return;

            List<bool> doorStateList = JsonConvert.DeserializeObject<List<bool>>(doorState.ToString());

            for (int i = 0; i < doorStateList.Count; i++)
            {
                if (doorStateList[i])
                {
                    vehicle.SetDoorOpen(i, false, false);
                }
                else
                {
                    vehicle.SetDoorShut(i, true);
                }
            }
        }
    }
}
