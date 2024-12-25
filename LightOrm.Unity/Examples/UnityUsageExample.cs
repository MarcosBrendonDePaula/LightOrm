using UnityEngine;
using System.Threading.Tasks;
using LightOrm.Core.Examples;
using LightOrm.Unity.Database;

namespace LightOrm.Unity.Examples
{
    public class UnityUsageExample : MonoBehaviour
    {
        private async void Start()
        {
            // The DatabaseManager is a singleton that handles the database connection
            // You can configure the connection settings in the Unity Inspector
            var dbManager = DatabaseManager.Instance;

            try
            {
                // Initialize database tables
                var playerTable = new PlayerModel();
                var itemTable = new ItemModel();
                var playerItemTable = new PlayerItemModel();

                await playerTable.EnsureTableExistsAsync(dbManager.GetConnection());
                await itemTable.EnsureTableExistsAsync(dbManager.GetConnection());
                await playerItemTable.EnsureTableExistsAsync(dbManager.GetConnection());

                Debug.Log("Database tables initialized successfully");

                // Create a new item
                var sword = new ItemModel
                {
                    Name = "Excalibur",
                    Description = "A legendary sword",
                    Rarity = "Legendary",
                    BaseValue = 1000
                };
                await sword.SaveAsync(dbManager.GetConnection());
                Debug.Log($"Created item: {sword.Name}");

                // Create a new player
                var newPlayer = new PlayerModel
                {
                    Username = "Arthur",
                    Email = "arthur@camelot.com",
                    Level = 1,
                    ExperiencePoints = 0,
                    LastLogin = System.DateTime.UtcNow,
                    IsActive = true
                };
                await newPlayer.SaveAsync(dbManager.GetConnection());
                Debug.Log($"Created player: {newPlayer.Username}");

                // Give the player the sword
                var playerSword = new PlayerItemModel
                {
                    PlayerId = newPlayer.Id,
                    ItemId = sword.Id,
                    Quantity = 1
                };
                await playerSword.SaveAsync(dbManager.GetConnection());
                Debug.Log($"Gave {sword.Name} to {newPlayer.Username}");

                // Load player with related items
                var loadedPlayer = await PlayerModel.FindByIdAsync(dbManager.GetConnection(), newPlayer.Id, includeRelated: true);
                if (loadedPlayer != null)
                {
                    Debug.Log($"Loaded player: {loadedPlayer.Username} (Level {loadedPlayer.Level})");
                    if (loadedPlayer.Items != null)
                    {
                        foreach (var inventoryItem in loadedPlayer.Items)
                        {
                            Debug.Log($"Inventory: {inventoryItem.Item?.Name} x{inventoryItem.Quantity}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Database operation failed: {ex.Message}");
            }
        }
    }
}
