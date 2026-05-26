using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;
using static UnityEngine.ParticleSystem;

namespace tarkin.Director.Bep.VideoSpecificScripts.jetengine
{
#if KJHBKREJHEVKJGRLKJHCGKJHR
    internal class Patch_ParticleSystemAdapter_Emit : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ParticleSystem), nameof(ParticleSystem.Emit), [typeof(EmitParams), typeof(int)]);
        }

        [PatchPostfix]
        private static void PatchPostfix(ParticleSystem __instance)
        {
            var externalForces = __instance.externalForces;
            externalForces.enabled = true;
        }
    }

    internal class Patch_ParticleSystemAdapter_Emit2 : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ParticleSystem), nameof(ParticleSystem.Emit), [typeof(Particle)]);
        }

        [PatchPostfix]
        private static void PatchPostfix(ParticleSystem __instance)
        {
            var externalForces = __instance.externalForces;
            externalForces.enabled = true;
        }
    }
    internal class Patch_ParticleSystemAdapter_Play : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ParticleSystem), nameof(ParticleSystem.Play), [typeof(bool)]);
        }

        [PatchPostfix]
        private static void PatchPostfix(ParticleSystem __instance, bool withChildren)
        {

            if (withChildren)
            {
                ParticleSystem[] systems = __instance.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in systems)
                {
                    var externalForces = ps.externalForces;
                    externalForces.enabled = true;
                }
            }
        }
    }
    internal class Patch_ParticleSystemAdapter_Play2 : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ParticleSystem), nameof(ParticleSystem.Play));
        }

        [PatchPostfix]
        private static void PatchPostfix(ParticleSystem __instance)
        {
            var externalForces = __instance.externalForces;
            externalForces.enabled = true;
        }
    }

    internal class sjhdgfhdfgh : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(SimpleSparksRenderer), nameof(SimpleSparksRenderer.EmitSeg));
        }

        [PatchPrefix]
        private static void PatchPrefix(SimpleSparksRenderer __instance, ref Vector3 velocity)
        {
            velocity += new Vector3(0,0, -10);
        }
    }
#endif
}
