#if !NO_UNITY
using System;
using System.Threading.Tasks;
using LightOrm.Core.Attributes;
using LightOrm.Core.Models;
using LightOrm.Unity.Database;
using UnityEngine;

namespace LightOrm.Unity.Examples
{
    // Modelos de exemplo — coloque os seus em arquivos próprios.

    public class PlayerSave : BaseModel<PlayerSave, int>
    {
        public override string TableName => "player_save";

        [Column("name", length: 50)]
        [Required]
        public string Name { get; set; }

        [Column("level")]
        public int Level { get; set; }

        [Column("xp")]
        public long Xp { get; set; }

        [OneToMany("player_id", typeof(InventoryItem), cascade: true, cascadeDelete: true)]
        public InventoryItem[] Inventory { get; set; }
    }

    public class InventoryItem : BaseModel<InventoryItem, int>
    {
        public override string TableName => "inventory_item";

        [Column("name", length: 50)]
        public string Name { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("player_id")]
        public int PlayerId { get; set; }
    }

    /// <summary>
    /// Exemplo end-to-end: configure um GameObject com DatabaseManager
    /// (provider = Sqlite, file = saves.db) e adicione esse script.
    /// Ao rodar, ele cria as tabelas, salva um player com inventário em
    /// cascata, e lê de volta com eager loading.
    /// </summary>
    public class UnityUsageExample : MonoBehaviour
    {
        private async void Start()
        {
            try
            {
                var db = DatabaseManager.Instance;
                await db.InitializeAsync();

                // Sintaxe simples (TId descoberto via reflexão):
                var players = await db.GetRepositoryAsync<PlayerSave>();
                var items   = await db.GetRepositoryAsync<InventoryItem>();
                // Ou explícito (útil quando você quer passar IRepository<T,TId> adiante):
                // var players = await db.GetRepositoryAsync<PlayerSave, int>();

                await players.EnsureSchemaAsync();
                await items.EnsureSchemaAsync();

                // Cria player com inventário em cascata.
                var arthur = new PlayerSave
                {
                    Name = "Arthur",
                    Level = 1,
                    Xp = 0,
                    Inventory = new[]
                    {
                        new InventoryItem { Name = "Excalibur", Quantity = 1 },
                        new InventoryItem { Name = "Healing Potion", Quantity = 5 }
                    }
                };
                await players.SaveAsync(arthur);
                Debug.Log($"[Example] Player {arthur.Name} salvo com id {arthur.Id} e " +
                          $"{arthur.Inventory.Length} itens.");

                // Lê com eager loading.
                var loaded = await players.FindByIdAsync(arthur.Id, includeRelated: true);
                Debug.Log($"[Example] Recarregado: {loaded.Name} (lvl {loaded.Level})");
                foreach (var item in loaded.Inventory)
                    Debug.Log($"[Example]   Inventário: {item.Name} x{item.Quantity}");

                // Query builder portável.
                var rares = await items.Query()
                    .Where(nameof(InventoryItem.Name), "LIKE", "Excalibur%")
                    .ToListAsync();
                Debug.Log($"[Example] {rares.Count} item(ns) raros encontrados.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Example] Falha: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
#endif
