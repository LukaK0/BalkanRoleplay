using GTANetworkAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RAGE;
using Currency;
using Data;
using Data.Extended;
using Data.Persistent;
using Data.Temporary;
using messages.error;
using messages.general;
using messages.help;
using messages.information;
using messages.success;
using Utility;
using vehicles;
using static Utility.Enumerators;

namespace Buildings
{
    public class DrivingSchool : Script
    {
        public static TextLabel DrivingSchoolTextLabel;
        private static int lastUpdate = Environment.TickCount;
        private static Dictionary<int, Timer> DrivingSchoolTimerList;

        public DrivingSchool()
        {
            DrivingSchoolTimerList = new Dictionary<int, Timer>();
        }

        public static void AddDrivingSchool()
        {
            // kreiranje colshape
            Entities.Colshapes.CreateEntity = (NetHandle handle) => GenerateDrivingSchoolColShape(handle);

            // labela
            NAPI.TextLabel.CreateTextLabel(GenRes.driving_school, Coordinates.DrivingSchool, 10.0f, 0.5f, 4, new Color(255, 255, 255), false, 0);

            // auto skola
            NAPI.Blip.CreateBlip(525, Coordinates.DrivingSchool, 1.0f, 0, GenRes.driving_school, 255, 0, true);
            NAPI.ColShape.CreateCylinderColShape(Coordinates.DrivingSchool, 2.5f, 1.0f);

        }

        public static void OnPlayerDisconnected(Player player)
        {
            if (!DrivingSchoolTimerList.TryGetValue(player.Value, out Timer drivingSchoolTimer)) return;

            drivingSchoolTimer.Dispose();
            DrivingSchoolTimerList.Remove(player.Value);
        }

        private static ColShape GenerateDrivingSchoolColShape(NetHandle handle)
        {
            return new ColShapeModel(handle)
            {
                LinkedType = ColShapeTypes.DrivingSchool,
                InstructionalButton = HelpRes.action_tramitate
            };
        }


        private void OnDrivingTimer(object playerObject)
        {
            Player player = (Player)playerObject;

            PlayerTemporaryModel data = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

            FinishDrivingExam(player, data.LastVehicle);

            if (DrivingSchoolTimerList.TryGetValue(player.Value, out Timer drivingSchoolTimer))
            {
                drivingSchoolTimer.Dispose();
                DrivingSchoolTimerList.Remove(player.Value);
            }

            player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.license_failed_not_in_vehicle);
        }

        private void FinishDrivingExam(Player player, Vehicle vehicle)
        {
            VehicleModel vehModel = Vehicles.GetVehicleById<VehicleModel>(vehicle.GetData<int>(EntityData.VehicleId));

            vehicle.Repair();
            vehicle.Position = vehModel.Position;
            vehicle.Rotation = new Vector3(0.0f, 0.0f, vehModel.Heading);

            if (player.VehicleSeat == (int)VehicleSeat.Driver && player.Vehicle == vehicle)
            {
                player.TriggerEvent("deleteLicenseCheckpoint");
            }

            PlayerTemporaryModel data = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

            data.LastVehicle = null;
            data.DrivingExam = DrivingExams.None;
            data.DrivingCheckpoint = 0;

            player.WarpOutOfVehicle();
        }

        public static int GetPlayerLicenseStatus(Player player, DrivingLicenses license)
        {
            return Array.ConvertAll(player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Licenses.Split(','), int.Parse)[(int)license];
        }

        public static void SetPlayerLicense(Player player, int license, int value)
        {
            CharacterModel model = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database);

            string[] licenses = model.Licenses.Split(',');

            licenses[license] = value.ToString();
            model.Licenses = string.Join(",", licenses);
        }

        public static void ShowDrivingLicense(Player player, Player target)
        {
            int currentLicense = 0;
            string playerLicenses = player.GetExternalData<CharacterModel>((int)ExternalDataSlot.Database).Licenses;
            int[] playerLicensesArray = Array.ConvertAll(playerLicenses.Split(','), int.Parse);

            foreach (int license in playerLicensesArray)
            {
                switch (currentLicense)
                {
                    case (int)DrivingLicenses.Car:
                        switch (license)
                        {
                            case -1:
                                target.SendChatMessage(Constants.COLOR_HELP + InfoRes.car_license_not_available);
                                break;

                            case 0:
                                target.SendChatMessage(Constants.COLOR_HELP + InfoRes.car_license_practical_pending);
                                break;

                            default:
                                target.SendChatMessage(Constants.COLOR_HELP + string.Format(InfoRes.car_license_points, license));
                                break;
                        }
                        break;

                    case (int)DrivingLicenses.Motorcycle:
                        switch (license)
                        {
                            case -1:
                                target.SendChatMessage(Constants.COLOR_HELP + InfoRes.motorcycle_license_not_available);
                                break;

                            case 0:
                                target.SendChatMessage(Constants.COLOR_HELP + InfoRes.motorcycle_license_practical_pending);
                                break;

                            default:
                                target.SendChatMessage(Constants.COLOR_HELP + string.Format(InfoRes.motorcycle_license_points, license));
                                break;
                        }
                        break;

                    case (int)DrivingLicenses.Taxi:
                        target.SendChatMessage(Constants.COLOR_HELP + (license == -1 ? InfoRes.taxi_license_not_available : InfoRes.taxi_license_up_to_date));
                        break;
                }
                currentLicense++;
            }
        }

        public static void OnPlayerEnterVehicle(Player player, Vehicle vehicle)
        {
            PlayerTemporaryModel data = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);
            VehicleModel vehModel = Vehicles.GetVehicleById<VehicleModel>(vehicle.GetData<int>(EntityData.VehicleId));
            player.TriggerEvent("initializeSpeedometer", vehModel.Kms, vehModel.Gas, vehicle.EngineStatus);
            switch (data.DrivingExam)
            {
                case DrivingExams.None:
                    player.WarpOutOfVehicle();
                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.not_in_car_practice);
                    break;

                case DrivingExams.CarPractical:
                    if (vehicle.Class == (int)VehicleClass.Sedan)
                    {
                        if (DrivingSchoolTimerList.TryGetValue(player.Value, out Timer drivingSchoolTimer))
                        {
                            drivingSchoolTimer.Dispose();
                            DrivingSchoolTimerList.Remove(player.Value);
                        }

                        data.LastVehicle = vehicle;
                        player.TriggerEvent("showLicenseCheckpoint", Coordinates.CarLicenseCheckpoints[data.DrivingCheckpoint], Coordinates.CarLicenseCheckpoints[data.DrivingCheckpoint + 1], CheckpointType.CylinderSingleArrow);
                    }
                    else
                    {
                        player.WarpOutOfVehicle();
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_driving_not_suitable);
                    }
                    break;

                case DrivingExams.MotorcyclePractical:
                    if (vehicle.Class == (int)VehicleClass.Motorcycle)
                    {
                        if (DrivingSchoolTimerList.TryGetValue(player.Value, out Timer drivingSchoolTimer) == true)
                        {
                            drivingSchoolTimer.Dispose();
                            DrivingSchoolTimerList.Remove(player.Value);
                        }

                        data.LastVehicle = vehicle;
                        player.TriggerEvent("showLicenseCheckpoint", Coordinates.BikeLicenseCheckpoints[data.DrivingCheckpoint], Coordinates.BikeLicenseCheckpoints[data.DrivingCheckpoint + 1], CheckpointType.CylinderSingleArrow);
                    }
                    else
                    {
                        player.WarpOutOfVehicle();
                        player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.vehicle_driving_not_suitable);
                    }
                    break;
            }
        }

        [ServerEvent(Event.PlayerExitVehicle)]
        public void PlayerExitVehicleServerEvent(Player player, Vehicle vehicle)
        {
            // Provera da li vozilo nije unisteno
            if (vehicle == null || !vehicle.Exists) return;

            PlayerTemporaryModel data = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

            if (data.DrivingExam != DrivingExams.None || data.LastVehicle != vehicle) return;

            if (Vehicles.GetVehicleById<VehicleModel>(vehicle.GetData<int>(EntityData.VehicleId)).Faction == (int)PlayerFactions.DrivingSchool)
            {
                player.SendChatMessage(Constants.COLOR_INFO + string.Format(InfoRes.license_vehicle_exit, 15));

                player.TriggerEvent("deleteLicenseCheckpoint");

                // Vreme ispita, pada se ako se premasi
                Timer drivingSchoolTimer = new Timer(OnDrivingTimer, player, 15000, Timeout.Infinite);
                DrivingSchoolTimerList.Add(player.Value, drivingSchoolTimer);
            }
        }

        [ServerEvent(Event.VehicleDamage)]
        public void VehicleDamageServerEvent(Vehicle vehicle, float lossFirst, float _)
        {
            Player player = (Player)NAPI.Vehicle.GetVehicleDriver(vehicle);

            if (player == null || !player.Exists) return;

            PlayerTemporaryModel data = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

            if (data.DrivingExam == DrivingExams.CarPractical || data.DrivingExam == DrivingExams.MotorcyclePractical)
            {
                // dovoljno stete?
                if (lossFirst - vehicle.Health < 5.0f) return;

                FinishDrivingExam(player, vehicle);

                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.license_drive_failed);
            }
        }

        [ServerEvent(Event.Update)]
        public void UpdateServerEvent()
        {
            if (Environment.TickCount - lastUpdate < 500) return;

            lastUpdate = Environment.TickCount;

            // Svi koji polazu
            Player[] licenseDrivers = NAPI.Pools.GetAllPlayers().Where(d => d.VehicleSeat == (int)VehicleSeat.Driver && d.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame).DrivingExam != DrivingExams.None).ToArray();

            foreach (Player player in licenseDrivers)
            {
                VehicleModel vehicle = Vehicles.GetVehicleById<VehicleModel>(player.Vehicle.GetData<int>(EntityData.VehicleId));

                if (vehicle.Faction != (int)PlayerFactions.DrivingSchool) continue;

                Vector3 velocity = NAPI.Entity.GetEntityVelocity(player.Vehicle);
                double speed = Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y + velocity.Z * velocity.Z);

                //Obori ako divlja
                if (Math.Round(speed * 3.6f) > Constants.MAX_DRIVING_VEHICLE)
                {
                    FinishDrivingExam(player, player.Vehicle);

                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.license_drive_failed);
                }
            }
        }

        [RemoteEvent("LoadLicenseSteps")]
        private async Task LoadLicenseStepsRemoteEvent(Player player, int license)
        {
            PlayerTemporaryModel data = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

            data.LicenseSelected = license;
            int licenseStatus = GetPlayerLicenseStatus(player, (DrivingLicenses)license);

            switch (licenseStatus)
            {
                case -1:
                    if (!Money.SubstractPlayerMoney(player, (int)Prices.DrivingTheorical, out string theoricalError))
                    {
                        //Ukloni iskacuci prozor
                        player.TriggerEvent("RemoveBrowserModule");

                        player.SendChatMessage(Constants.COLOR_ERROR + theoricalError);
                        break;
                    }

                    //Biranje pitanja i odgovora
                    List<TestModel> answers = new List<TestModel>();
                    List<TestModel> questions = await DatabaseOperations.GetRandomQuestions(license + 1, Constants.MAX_LICENSE_QUESTIONS).ConfigureAwait(false);

                    foreach (TestModel question in questions)
                    {
                        answers.AddRange(await DatabaseOperations.GetQuestionAnswers(question.id).ConfigureAwait(false));
                    }

                    PlayerTemporaryModel tempModel = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);
                    tempModel.LicenseType = (DrivingLicenses)license;
                    tempModel.LicenseQuestion = 0;

                    player.TriggerEvent("startLicenseExam", questions, answers);

                    break;

                case 0:
                    if (!Money.SubstractPlayerMoney(player, (int)Prices.DrivingPractical, out string practicalError))
                    {
                        player.TriggerEvent("RemoveBrowserModule");

                        player.SendChatMessage(Constants.COLOR_ERROR + practicalError);

                        break;
                    }

                    data.LicenseType = (DrivingLicenses)license;
                    data.DrivingExam = data.LicenseType == DrivingLicenses.Car ? DrivingExams.CarPractical : DrivingExams.MotorcyclePractical;
                    data.DrivingCheckpoint = 0;

                    player.SendChatMessage(Constants.COLOR_INFO + (data.DrivingExam == DrivingExams.CarPractical ? InfoRes.enter_license_car_vehicle : InfoRes.enter_license_bike_vehicle));

                    player.TriggerEvent("RemoveBrowserModule");

                    break;

                default:
                    player.TriggerEvent("RemoveBrowserModule");

                    player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.player_already_license);

                    break;
            }
        }

        [RemoteEvent("checkAnswer")]
        public void CheckAnswerRemoteEvent(Player player, int answer)
        {
            PlayerTemporaryModel playerModel = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

            if (DatabaseOperations.CheckAnswerCorrect(answer))
            {
                int nextQuestion = playerModel.LicenseQuestion + 1;

                if (nextQuestion < Constants.MAX_LICENSE_QUESTIONS)
                {
                    playerModel.LicenseQuestion = nextQuestion;
                    player.TriggerEvent("getNextTestQuestion");
                }
                else
                {
                    SetPlayerLicense(player, (int)playerModel.LicenseType, 0);

                    playerModel.LicenseType = DrivingLicenses.None;
                    playerModel.LicenseQuestion = 0;

                    player.SendChatMessage(Constants.COLOR_SUCCESS + SuccRes.license_exam_passed);

                    player.TriggerEvent("finishLicenseExam");

                    player.TriggerEvent("RemoveBrowserModule");
                    PlayerTemporaryModel data = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);
                    if (!Money.SubstractPlayerMoney(player, (int)Prices.DrivingPractical, out string practicalError))
                    {
                        player.TriggerEvent("RemoveBrowserModule");

                        player.SendChatMessage(Constants.COLOR_ERROR + practicalError);

                        return;
                    }

                    data.LicenseType = (DrivingLicenses)data.LicenseSelected;
                    data.DrivingExam = data.LicenseType == DrivingLicenses.Car ? DrivingExams.CarPractical : DrivingExams.MotorcyclePractical;
                    data.DrivingCheckpoint = 0;

                    player.SendChatMessage(Constants.COLOR_INFO + (data.DrivingExam == DrivingExams.CarPractical ? InfoRes.enter_license_car_vehicle : InfoRes.enter_license_bike_vehicle));

                }
            }
            else
            {
                player.SendChatMessage(Constants.COLOR_ERROR + ErrRes.license_exam_failed);

                playerModel.LicenseType = DrivingLicenses.None;
                playerModel.LicenseQuestion = 0;

                player.TriggerEvent("finishLicenseExam");
            }
        }

        [RemoteEvent("licenseCheckpointReached")]
        public void LicenseCheckpointReachedRemoteEvent(Player player)
        {
            VehicleModel vehicle = Vehicles.GetVehicleById<VehicleModel>(player.Vehicle.GetData<int>(EntityData.VehicleId));

            if (vehicle.Faction != (int)PlayerFactions.DrivingSchool) return;

            int license = 0;
            List<Vector3> checkpointList = new List<Vector3>();

            PlayerTemporaryModel data = player.GetExternalData<PlayerTemporaryModel>((int)ExternalDataSlot.Ingame);

            if (data.DrivingExam == DrivingExams.CarPractical)
            {
                license = (int)DrivingLicenses.Car;
                checkpointList = Coordinates.CarLicenseCheckpoints.ToList();
            }
            else if (data.DrivingExam == DrivingExams.MotorcyclePractical)
            {
                license = (int)DrivingLicenses.Motorcycle;
                checkpointList = Coordinates.BikeLicenseCheckpoints.ToList();
            }

            int checkpointNumber = data.DrivingCheckpoint;
            data.DrivingCheckpoint++;

            if (checkpointNumber < checkpointList.Count - 2)
            {
                player.TriggerEvent("showLicenseCheckpoint", checkpointList[checkpointNumber + 1], checkpointList[checkpointNumber + 2], CheckpointType.CylinderSingleArrow);
            }
            else if (checkpointNumber == checkpointList.Count - 2)
            {
                player.TriggerEvent("showLicenseCheckpoint", checkpointList[checkpointNumber + 1], vehicle.Position, CheckpointType.CylinderSingleArrow);
            }
            else if (checkpointNumber == Coordinates.CarLicenseCheckpoints.Length - 1)
            {
                player.TriggerEvent("showLicenseCheckpoint", vehicle.Position.Subtract(new Vector3(0.0f, 0.0f, 0.4f)), new Vector3(), CheckpointType.CylinderCheckerboard);
            }
            else
            {
                FinishDrivingExam(player, player.Vehicle);

                // Sa koliko bodova je polozio
                SetPlayerLicense(player, license, 12);

                player.SendChatMessage(Constants.COLOR_SUCCESS + SuccRes.license_drive_passed);
            }
        }
    }
}
