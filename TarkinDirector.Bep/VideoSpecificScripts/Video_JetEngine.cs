using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using Sirenix.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using Systems.Effects;
using tarkin.Director.Shared;
using UnityEngine;

namespace tarkin.Director.Bep.VideoSpecificScripts
{
    internal class Video_JetEngine : IDisposable
    {
        readonly Vector3 TurbineForward = new Vector3(0,0,1f);

        internal Video_JetEngine()
        {
            PhysicsTriggerStaticBroadcaster.OnEnter += PhysicsTriggerStaticBroadcaster_OnEnter;

            if (Singleton<GameWorld>.Instantiated)
            {
                Singleton<GameWorld>.Instance.MainPlayer.ActiveHealthController.ChangeHydration(UnityEngine.Random.Range(70,100));
                Singleton<GameWorld>.Instance.MainPlayer.ActiveHealthController.ChangeEnergy(UnityEngine.Random.Range(70,100));
            }
        }

        private void PhysicsTriggerStaticBroadcaster_OnEnter(PhysicsTriggerStaticBroadcaster instance, Collider other)
        {
            Plugin.Logger.LogInfo(other.name + " entered");

            if (other.TryGetComponent<LootItem>(out var lootItem))
            {
                Vector3 normal = TurbineForward;

                Emit(GetEffectByName("Metal"), other.transform.position, normal);

                string lootEffectName = "Metal";

                Item item = lootItem.Item;

                float weightFactor = Mathf.Lerp(1, 3f, Mathf.InverseLerp(0, 30, item.Weight));
                normal *= weightFactor;

                if (item is FuelItemClass or LubricantItemClass)
                {
                    lootEffectName = "Gas_explosion";
                    normal /= 2f;
                }

                if (TryGetEffectNameBySpecificTemplate(item.StringTemplateId, out string potentialEffectName))
                {
                    lootEffectName = potentialEffectName;
                }

                Effects.Effect effect = GetEffectByName(lootEffectName);

                Emit(effect, other.transform.position, normal);

                Emit(effect, other.transform.position - TurbineForward * 5f, -normal * 12f);

                Plugin.Logger.LogInfo($"{item.GetType()} emitted with {normal}");

                lootItem.transform.position += new Vector3(0, 2, 25);
            }
        }

        private bool TryGetEffectNameBySpecificTemplate(string itemTemplateId, out string effectName)
        {
            string GetEffectNameByTemplateId()
            {
                switch (itemTemplateId)
                {
                    case "676bf44c5539167c3603e869":
                        return "Fire_extinguished";

                    case "590a3cd386f77436f20848cb":
                    case "590a3d9c86f774385926e510":
                        return "Gaslamp";

                    case "590c5a7286f7747884343aea":
                    case "5d6fc78386f77449d825f9dc":
                    case "5d6fc87386f77449db3db94e":

                    case "60391a8b3364dc22b04d0ce5":
                        return "smallgrenade_expl";

                    case "5d1b376e86f774252519444e":
                        return "Glass";

                    default:
                        return null;
                }
            }

            effectName = GetEffectNameByTemplateId();
            if (effectName != null)
                return true;

            return false;
        }

        private void Emit(Effects.Effect effect, Vector3 pos, Vector3 normal)
        {
            if (effect == null)
                return;

            Singleton<Effects>.Instance.AddEffectEmit(
                effect,
                position: pos,
                normal: normal,
                hitCollider: null,
                withDecal: false,
                volume: 1f);
        }

        private Effects.Effect GetEffectByName(string name)
        {
            return Singleton<Effects>.Instance.EffectsArray.Where(e => e.Name == name).FirstOrDefault();
        }
        public void Dispose()
        {
            PhysicsTriggerStaticBroadcaster.OnEnter -= PhysicsTriggerStaticBroadcaster_OnEnter;
        }
    }
}
