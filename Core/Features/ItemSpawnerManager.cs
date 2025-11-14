using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mimic.Actors;
using MimicAPI.GameAPI;

namespace Mimesis_Mod_Menu.Core.Features
{
    public class ItemSpawnerManager : FeatureManager
    {
        private Dictionary<int, int> pendingItemsToSpawn = new Dictionary<int, int>();

        public void AddItemToSpawn(int itemMasterID, int quantity = 1)
        {
            try
            {
                if (pendingItemsToSpawn.ContainsKey(itemMasterID))
                    pendingItemsToSpawn[itemMasterID] += quantity;
                else
                    pendingItemsToSpawn[itemMasterID] = quantity;

                LogMessage($"Queued item {itemMasterID} x{quantity}");
            }
            catch (Exception ex)
            {
                LogError(nameof(AddItemToSpawn), ex);
            }
        }

        public Dictionary<int, int> GetPendingItems() => new Dictionary<int, int>(pendingItemsToSpawn);

        public void ClearPendingItems() => pendingItemsToSpawn.Clear();

        public override void Update()
        {
            if (pendingItemsToSpawn.Count == 0)
                return;

            try
            {
                ProtoActor localPlayer = PlayerAPI.GetLocalPlayer();
                if (localPlayer == null)
                    return;

                var itemsToProcess = pendingItemsToSpawn.ToList();
                foreach (var kvp in itemsToProcess)
                {
                    int itemMasterID = kvp.Key;
                    int quantity = kvp.Value;

                    ItemSpawnerPatches.SetItemToSpawn(itemMasterID, quantity);
                    pendingItemsToSpawn.Remove(itemMasterID);
                }
            }
            catch (Exception ex)
            {
                LogError(nameof(Update), ex);
            }
        }
    }
}
