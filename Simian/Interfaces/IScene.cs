/*
 * Copyright (c) Open Metaverse Foundation
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. The name of the author may not be used to endorse or promote products
 *    derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 * THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Simian
{
    #region Enums

    /// <summary>
    /// Specifies that fields that have changed in a call to IScene.EntityAddOrUpdate
    /// </summary>
    [Flags]
    public enum UpdateFlags : uint
    {
        Position = 1 << 0,
        Rotation = 1 << 1,
        Velocity = 1 << 2,
        Acceleration = 1 << 3,
        AngularVelocity = 1 << 4,
        CollisionPlane = 1 << 5,
        Scale = 1 << 6,
        Shape = 1 << 7,
        PhysicalStatus = 1 << 8,
        PhantomStatus = 1 << 9,
        Parent = 1 << 10,
        Serialize = 1 << 11,
        FullUpdate = UInt32.MaxValue
    }

    public enum EntityChatType
    {
        Normal = 0,
        Owner = 1,
        Debug = 2,
        Broadcast = 3
    }

    public static class UpdateFlagsExtensions
    {
        public static bool HasFlag(this UpdateFlags updateFlags, UpdateFlags flag)
        {
            return (updateFlags & flag) == flag;
        }
    }

    public static class PrimFlagsExtensions
    {
        public static bool HasFlag(this PrimFlags primFlags, PrimFlags flag)
        {
            return (primFlags & flag) == flag;
        }
    }

    #endregion Enums

    #region Delegates

    public class EntityAddOrUpdateArgs : EventArgs
    {
        public ISceneEntity Entity;
        public UpdateFlags UpdateFlags;
        public uint ExtraFlags;
        public bool IsNew;
    }

    public class EntityArgs : EventArgs
    {
        public ISceneEntity Entity;
    }

    public class PresenceArgs : EventArgs
    {
        public IScenePresence Presence;
    }

    public class PhysicalPresenceArgs : EventArgs
    {
        public IPhysicalPresence Presence;
    }

    public class ChatArgs : EventArgs
    {
        public ISceneEntity Source;
        public float AudibleDistance;
        public string Message;
        public int Channel;
        public EntityChatType Type;
    }

    public class PresenceAlertArgs : EventArgs
    {
        public IScenePresence Presence;
        public string Message;
    }

    public class EntitySignificantMovementArgs : EventArgs
    {
        public ISceneEntity Entity;
        public Vector3 OldRelativePosition;
    }

    #endregion Delegates

    public interface IScene
    {
        event EventHandler<EntityAddOrUpdateArgs> OnEntityAddOrUpdate;
        event EventHandler<EntityArgs> OnEntityRemove;
        event EventHandler<EntitySignificantMovementArgs> OnEntitySignificantMovement;
        event EventHandler<ChatArgs> OnEntityChat;
        event EventHandler<PresenceArgs> OnPresenceAdd;
        event EventHandler<PresenceArgs> OnPresenceRemove;
        event EventHandler<PhysicalPresenceArgs> OnSendPresenceAnimations;
        event EventHandler<PresenceAlertArgs> OnPresenceAlert;

        bool IsRunning { get; }
        Simian Simian { get; }
        UUID ID { get; }
        string Name { get; }
        Vector3d MinPosition { get; }
        Vector3d MaxPosition { get; }
        IConfigSource Config { get; }
        OSDMap ExtraData { get; }
        /// <summary>Capability router</summary>
        CapabilityRouter Capabilities { get; }

        void Start();
        void Stop();

        T GetSceneModule<T>();

        uint CreateLocalID();

        void AddNeighbor(SceneInfo neighbor);
        bool RemoveNeighbor(UUID neighborID);
        SceneInfo[] GetNeighbors();
        SceneInfo[] GetNeighborsNear(Vector3d position, double radius);

        void AddPublicCapability(string capName, Uri uri);
        bool RemovePublicCapability(string capName);
        bool TryGetPublicCapability(string capName, out Uri uri);

        void AddCommandHandler(string command, CommandCallback callback);
        bool RemoveCommandHandler(string command);
        bool HandleCommand(string command, string[] args);
        string[] GetCompletions(string complete);
        bool HelpHandler(string command, string[] args);

        bool TryGetApiMethod(string methodName, out ApiMethod apiMethod);

        void AddInterestListHandler(string eventType, InterestListEventHandler handler);
        void CreateInterestListEvent(InterestListEvent eventData);
        void CreateInterestListEventFor(IScenePresence presence, InterestListEvent eventData);
        bool TryGetInterestListHandler(string eventType, out InterestListEventHandler handler);

        void EntityAddOrUpdate(object sender, ISceneEntity entity, UpdateFlags updateFlags, uint extraFlags);
        bool EntityRemove(object sender, ISceneEntity entity);
        void EntityChat(object sender, ISceneEntity source, float audibleDistance, string message, int channel, EntityChatType type);

        void PresenceAlert(object sender, IScenePresence presence, string message);

        bool ContainsEntity(UUID id);
        bool ContainsEntity(uint localID);
        int EntityCount();
        bool TryGetEntity(UUID id, out ISceneEntity entity);
        bool TryGetEntity(uint localID, out ISceneEntity entity);
        void ForEachEntity(Action<ISceneEntity> action);
        ISceneEntity FindEntity(Predicate<ISceneEntity> predicate);

        void SendPresenceAnimations(object sender, IPhysicalPresence presence);

        bool CanPresenceEnter(UUID presenceID, ref Vector3 startPosition, ref Vector3 lookAt);
        bool CanPresenceSee(UUID presenceID);
        int PresenceCount();
        bool TryGetPresence(UUID id, out IScenePresence presence);
        void ForEachPresence(Action<IScenePresence> action);
        IScenePresence[] GetPresences();
        IScenePresence FindPresence(Predicate<IScenePresence> predicate);
        int RemoveAllPresences(Predicate<IScenePresence> predicate);
    }
}
