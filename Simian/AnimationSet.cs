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
using System.Threading;
using OpenMetaverse;

namespace Simian
{
    public struct Animation
    {
        public UUID ID;
        public int SequenceNum;

        public Animation(UUID id, int sequenceNum)
        {
            ID = id;
            SequenceNum = sequenceNum;
        }
    }

    public class AnimationSet
    {
        private Animation defaultAnimation;
        private List<Animation> animations = new List<Animation>();

        public AnimationSet()
        {
            ResetDefaultAnimation();
        }

        public bool HasAnimation(UUID animID)
        {
            if (defaultAnimation.ID == animID)
                return true;

            lock (animations)
            {
                for (int i = 0; i < animations.Count; ++i)
                {
                    if (animations[i].ID == animID)
                        return true;
                }
            }

            return false;
        }

        public bool Add(UUID animID, ref int sequenceCounter)
        {
            lock (animations)
            {
                if (!HasAnimation(animID))
                {
                    int sequenceNum = Interlocked.Increment(ref sequenceCounter);
                    animations.Add(new Animation(animID, sequenceNum));
                    return true;
                }
            }

            return false;
        }

        public bool Add(UUID animID, int sequenceNum)
        {
            lock (animations)
            {
                if (!HasAnimation(animID))
                {
                    animations.Add(new Animation(animID, sequenceNum));
                    return true;
                }
            }

            return false;
        }

        public bool Remove(UUID animID)
        {
            if (defaultAnimation.ID == animID)
            {
                ResetDefaultAnimation();
                return true;
            }
            else if (HasAnimation(animID))
            {
                lock (animations)
                {
                    for (int i = 0; i < animations.Count; i++)
                    {
                        if (animations[i].ID == animID)
                        {
                            animations.RemoveAt(i);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public void Clear()
        {
            ResetDefaultAnimation();
            lock (animations) animations.Clear();
        }

        public bool SetDefaultAnimation(UUID animID, ref int sequenceCounter)
        {
            if (defaultAnimation.ID != animID)
            {
                int sequenceNum = Interlocked.Increment(ref sequenceCounter);
                defaultAnimation = new Animation(animID, sequenceNum);
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool SetDefaultAnimation(UUID animID, int sequenceNum)
        {
            if (defaultAnimation.ID != animID)
            {
                defaultAnimation = new Animation(animID, sequenceNum);
                return true;
            }
            else
            {
                return false;
            }
        }

        public Animation[] GetAnimations()
        {
            lock (animations)
            {
                Animation[] triggers = new Animation[animations.Count + 1];

                triggers[0] = new Animation(defaultAnimation.ID, defaultAnimation.SequenceNum);

                for (int i = 0; i < animations.Count; i++)
                    triggers[i + 1] = new Animation(animations[i].ID, animations[i].SequenceNum);

                return triggers;
            }
        }

        public bool ResetDefaultAnimation()
        {
            return SetDefaultAnimation(Animations.STAND, 1);
        }
    }
}
