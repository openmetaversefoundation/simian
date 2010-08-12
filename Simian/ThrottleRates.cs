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
using Nini.Config;

namespace Simian
{
    /// <summary>
    /// Holds default drip rates and maximum burst rates for throttling with
    /// hierarchical token buckets. The maximum burst rates set here are hard
    /// limits and can not be overridden by client requests
    /// </summary>
    public class ThrottleRates
    {
        /// <summary>Drip rate for resent packets</summary>
        public readonly int Resend;
        /// <summary>Drip rate for terrain packets</summary>
        public readonly int Land;
        /// <summary>Drip rate for wind packets</summary>
        public readonly int Wind;
        /// <summary>Drip rate for cloud packets</summary>
        public readonly int Cloud;
        /// <summary>Drip rate for task packets</summary>
        public readonly int Task;
        /// <summary>Drip rate for texture packets</summary>
        public readonly int Texture;
        /// <summary>Drip rate for asset packets</summary>
        public readonly int Asset;

        /// <summary>Drip rate for the client token bucket</summary>
        public int ClientTotal;

        /// <summary>Maximum burst rate for resent packets</summary>
        public int ResendLimit;
        /// <summary>Maximum burst rate for land packets</summary>
        public int LandLimit;
        /// <summary>Maximum burst rate for wind packets</summary>
        public int WindLimit;
        /// <summary>Maximum burst rate for cloud packets</summary>
        public int CloudLimit;
        /// <summary>Maximum burst rate for task (state and transaction) packets</summary>
        public int TaskLimit;
        /// <summary>Maximum burst rate for texture packets</summary>
        public int TextureLimit;
        /// <summary>Maximum burst rate for asset packets</summary>
        public int AssetLimit;
        /// <summary>Burst rate for the client token bucket</summary>
        public int ClientTotalLimit;

        /// <summary>Drip rate for the scene</summary>
        public int SceneTotal;
        /// <summary>Burst rate for the scene</summary>
        public int SceneTotalLimit;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="mtu">Maximum transmission unit</param>
        /// <param name="throttleConfig">Config to load defaults from</param>
        public ThrottleRates(int mtu, IConfig throttleConfig)
        {
            Resend = 12500;
            Land = mtu;
            Wind = mtu;
            Cloud = mtu;
            Task = mtu;
            Texture = mtu;
            Asset = mtu;

            if (throttleConfig != null)
            {
                ResendLimit = throttleConfig.GetInt("ResendLimit", 18750);
                LandLimit = throttleConfig.GetInt("LandLimit", 29750);
                WindLimit = throttleConfig.GetInt("WindLimit", 18750);
                CloudLimit = throttleConfig.GetInt("CloudLimit", 18750);
                TaskLimit = throttleConfig.GetInt("TaskLimit", 55750);
                TextureLimit = throttleConfig.GetInt("TextureLimit", 55750);
                AssetLimit = throttleConfig.GetInt("AssetLimit", 27500);

                ClientTotal = throttleConfig.GetInt("ClientLimit", 0);
                ClientTotalLimit = ClientTotal;

                SceneTotal = throttleConfig.GetInt("SceneLimit", 0);
                SceneTotalLimit = SceneTotal;
            }
            else
            {
                ResendLimit = 18750;
                LandLimit = 29750;
                WindLimit = 18750;
                CloudLimit = 18750;
                TaskLimit = 55750;
                TextureLimit = 55750;
                AssetLimit = 27500;

                ClientTotal = 0;
                ClientTotalLimit = ClientTotal;

                SceneTotal = 0;
                SceneTotalLimit = SceneTotal;
            }
        }

        public int GetRate(ThrottleCategory type)
        {
            switch (type)
            {
                case ThrottleCategory.Resend:
                    return Resend;
                case ThrottleCategory.Land:
                    return Land;
                case ThrottleCategory.Wind:
                    return Wind;
                case ThrottleCategory.Cloud:
                    return Cloud;
                case ThrottleCategory.Task:
                    return Task;
                case ThrottleCategory.Texture:
                    return Texture;
                case ThrottleCategory.Asset:
                    return Asset;
                case ThrottleCategory.Unknown:
                default:
                    return 0;
            }
        }

        public int GetLimit(ThrottleCategory type)
        {
            switch (type)
            {
                case ThrottleCategory.Resend:
                    return ResendLimit;
                case ThrottleCategory.Land:
                    return LandLimit;
                case ThrottleCategory.Wind:
                    return WindLimit;
                case ThrottleCategory.Cloud:
                    return CloudLimit;
                case ThrottleCategory.Task:
                    return TaskLimit;
                case ThrottleCategory.Texture:
                    return TextureLimit;
                case ThrottleCategory.Asset:
                    return AssetLimit;
                case ThrottleCategory.Unknown:
                default:
                    return 0;
            }
        }
    }
}
