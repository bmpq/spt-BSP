using Comfort.Common;
using EFT;
using System;
using tarkin.Director.Shared;
using UnityEngine;

namespace tarkin.Director.Bep
{
    internal class Video_Sidorovich : IDisposable
    {
        private Player inZone;

        internal Video_Sidorovich()
        {
            PhysicsTriggerStaticBroadcaster.OnEnter += PhysicsTriggerStaticBroadcaster_OnEnter;
            PhysicsTriggerStaticBroadcaster.OnExit += PhysicsTriggerStaticBroadcaster_OnExit;

            if (Singleton<GameWorld>.Instance.MainPlayer.CharacterController.GetCollider() is CapsuleCollider capsuleCollider)
            {
                capsuleCollider.radius = 0.3f;
            }
        }

        private void PhysicsTriggerStaticBroadcaster_OnEnter(PhysicsTriggerStaticBroadcaster arg1, UnityEngine.Collider arg2)
        {
            Player playerByCollider = Singleton<GameWorld>.Instance.GetPlayerByCollider(arg2);
            if (playerByCollider != null)
            {
                inZone = playerByCollider;
                //playerByCollider.HideWeapon();
                NotificationManagerClass.DisplayMessageNotification("Time to visit this area is limited", EFT.Communications.ENotificationDurationType.Default, EFT.Communications.ENotificationIconType.Alert);
            }

        }

        private void PhysicsTriggerStaticBroadcaster_OnExit(PhysicsTriggerStaticBroadcaster arg1, UnityEngine.Collider arg2)
        {
            Player playerByCollider = Singleton<GameWorld>.Instance.GetPlayerByCollider(arg2);
            if (playerByCollider != null)
            {
                inZone = null;
                //playerByCollider.RevealWeapon();
            }

        }

        public void Dispose()
        {
            PhysicsTriggerStaticBroadcaster.OnEnter -= PhysicsTriggerStaticBroadcaster_OnEnter;
            PhysicsTriggerStaticBroadcaster.OnExit -= PhysicsTriggerStaticBroadcaster_OnExit;

            Singleton<GameWorld>.Instance.MainPlayer.RevealWeapon();

            if (Singleton<GameWorld>.Instance.MainPlayer.CharacterController.GetCollider() is CapsuleCollider capsuleCollider)
            {
                capsuleCollider.radius = 0.366f;
            }
        }

    }
}
