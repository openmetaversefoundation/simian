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
using System.ComponentModel.Composition;
using OpenMetaverse;
using Simian.Protocols.Linden;

namespace Simian.Scripting.Linden
{
    public partial class LindenApi : ISceneModule, IScriptApi
    {
        //llAllowInventoryDrop

        //llGetInventoryCreator

        //llGetInventoryKey

        [ScriptMethod]
        public string llGetInventoryName(IScriptInstance script, int type, int number)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return String.Empty;

            AssetType assetType = (AssetType)type;

            IList<LLInventoryTaskItem> items = prim.Inventory.FindAllItems(item => assetType == AssetType.Unknown || item.AssetType == assetType);
            if (items.Count >= number)
                return String.Empty;
            
            SortedList<string, LLInventoryTaskItem> sortedItems = new SortedList<string, LLInventoryTaskItem>(items.Count);
            for (int i = 0; i < items.Count; i++)
                sortedItems.Add(items[i].Name, items[i]);

            return sortedItems.Values[number].Name;
        }

        [ScriptMethod]
        public int llGetInventoryNumber(IScriptInstance script, int type)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return 0;

            AssetType assetType = (AssetType)type;
            return prim.Inventory.FindAllItems(item => assetType == AssetType.Unknown || item.AssetType == assetType).Count;
        }

        [ScriptMethod]
        public int llGetInventoryPermMask(IScriptInstance script, string name, int mask)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return 0;

            LLInventoryTaskItem found = prim.Inventory.FindItem(item => item.Name == name);
            if (found != null)
            {
                switch (mask)
                {
                    case LSLConstants.MASK_BASE:
                        return (int)found.Permissions.BaseMask;
                    case LSLConstants.MASK_OWNER:
                        return (int)found.Permissions.OwnerMask;
                    case LSLConstants.MASK_GROUP:
                        return (int)found.Permissions.GroupMask;
                    case LSLConstants.MASK_EVERYONE:
                        return (int)found.Permissions.EveryoneMask;
                    case LSLConstants.MASK_NEXT:
                        return (int)found.Permissions.NextOwnerMask;
                }
            }

            return 0;
        }

        [ScriptMethod]
        public int llGetInventoryType(IScriptInstance script, string name)
        {
            LLPrimitive prim = script.Host as LLPrimitive;
            if (prim == null)
                return LSLConstants.INVENTORY_NONE;

            LLInventoryTaskItem found = prim.Inventory.FindItem(item => item.Name == name);
            if (found != null)
                return (int)found.AssetType;

            return LSLConstants.INVENTORY_NONE;
        }

        //llGiveInventory

        //llGiveInventoryList

        //llRemoveInventory

        //llRequestInventoryData
    }
}
