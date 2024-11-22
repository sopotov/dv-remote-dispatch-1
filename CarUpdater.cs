using DV.LocoRestoration;
using DV.RemoteControls;
using DV.Simulation.Controllers;
using DV.ThingTypes;
using DV.Utils;
using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace DvMod.RemoteDispatch
{
    public static class CarUpdater
    {
        public static void MarkCarAsDirty(TrainCar car)
        {
            Sessions.AddTag($"carguid-{car.CarGUID}");
        }

        [HarmonyPatch(typeof(RemoteControllerModule), nameof(RemoteControllerModule.Init))]
        public static class RemoteControllerModuleInitPatch
        {
            public static void Postfix(RemoteControllerModule __instance)
            {
                var trainCar = TrainCar.Resolve(__instance.gameObject);
                var overrider = __instance.controlsOverrider;
                var controls = new OverridableBaseControl[]
                {
                    overrider.Brake,
                    overrider.IndependentBrake,
                    overrider.Reverser,
                    overrider.Throttle,
                };

                foreach (var control in controls.Where(c => c != null))
                {
                    control.ControlUpdated += _ => MarkCarAsDirty(trainCar);
                }
            }
        }

        public static void MarkTrainsetAsDirty(Trainset trainset)
        {
            Sessions.AddTag($"trainset-{trainset.id}");
        }

        [HarmonyPatch(typeof(TrainCar), nameof(TrainCar.Update))]
        public static class UpdatePatch
        {
            public static void Postfix(TrainCar __instance)
            {
                if (__instance.logicCar == null || __instance.isStationary)
                    return;
                MarkTrainsetAsDirty(__instance.trainset);
            }
        }

        public static void Start()
        {
            CarSpawner carSpawner = SingletonBehaviour<CarSpawner>.Instance;
            if (carSpawner == null)
            {
                Main.DebugLog(() => $"Tried to start {nameof(CarUpdater)} before {nameof(CarSpawner)} was initialized!");
                return;
            }

            carSpawner.CarSpawned += OnCarsChanged;
            carSpawner.CarAboutToBeDeleted += OnCarsChanged;

            foreach (var controller in LocoRestorationController.allLocoRestorationControllers)
                controller.StateChanged += OnRestorationStateChanged;
        }

        public static void Stop()
        {
            CarSpawner carSpawner = SingletonBehaviour<CarSpawner>.Instance;
            if (carSpawner == null)
                return;
            carSpawner.CarSpawned -= OnCarsChanged;
            carSpawner.CarAboutToBeDeleted -= OnCarsChanged;

            foreach (var controller in LocoRestorationController.allLocoRestorationControllers)
                controller.StateChanged -= OnRestorationStateChanged;
        }

        private static void OnCarsChanged(TrainCar trainCar)
        {
            Sessions.AddTag("cars");
        }

        private static void OnRestorationStateChanged(LocoRestorationController controller, TrainCarLivery livery, LocoRestorationController.RestorationState newState)
        {
            MarkCarAsDirty(controller.loco);
            if (controller.secondCar != null)
                MarkCarAsDirty(controller.secondCar);
        }
    }
}
