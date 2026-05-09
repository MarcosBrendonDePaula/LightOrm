using System;
using System.Threading.Tasks;
using LightOrm.Core.Sql;
using LightOrm.Core.Tests.Models;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LightOrm.Core.Tests.Lifecycle
{
    public class HookContextTests
    {
        private static (SqliteConnection conn, IDialect dialect) Open()
        {
            var c = new SqliteConnection("Data Source=:memory:");
            c.Open();
            return (c, new SqliteDialect());
        }

        private async Task<(SqlRepository<AuditedOrderModel, int> orders, SqlRepository<AuditEntryModel, int> audits, SqliteConnection conn)>
            Setup()
        {
            var (conn, dialect) = Open();
            var orders = new SqlRepository<AuditedOrderModel, int>(conn, dialect);
            var audits = new SqlRepository<AuditEntryModel, int>(conn, dialect);
            await orders.EnsureSchemaAsync();
            await audits.EnsureSchemaAsync();
            return (orders, audits, conn);
        }

        [Fact]
        public async Task Insert_audit_via_hook_context_lands_in_other_table()
        {
            var (orders, audits, _) = await Setup();
            var order = new AuditedOrderModel { Description = "first", Total = 10m };
            await orders.SaveAsync(order);

            var auditRows = await audits.FindAllAsync();
            Assert.Single(auditRows);
            Assert.Equal(nameof(AuditedOrderModel), auditRows[0].EntityType);
            Assert.Equal(order.Id, auditRows[0].EntityId);
        }

        [Fact]
        public async Task Update_audit_via_hook_context_appends_new_row()
        {
            var (orders, audits, _) = await Setup();
            var order = new AuditedOrderModel { Description = "v1", Total = 10m };
            await orders.SaveAsync(order);
            order.Description = "v2";
            order.Total = 20m;
            await orders.SaveAsync(order);

            var auditRows = await audits.FindAllAsync();
            // 1 do create + 1 do update.
            Assert.Equal(2, auditRows.Count);
        }

        [Fact]
        public async Task Audit_participates_in_same_transaction_as_save()
        {
            var (orders, audits, conn) = await Setup();

            using (var tx = conn.BeginTransaction())
            {
                var ordersTx = new SqlRepository<AuditedOrderModel, int>(conn, new SqliteDialect(), tx);
                var order = new AuditedOrderModel { Description = "rolled-back", Total = 99m };
                await ordersTx.SaveAsync(order);
                tx.Rollback();
            }

            // Tudo desfeito: nem order nem audit existem.
            Assert.Empty(await orders.FindAllAsync());
            Assert.Empty(await audits.FindAllAsync());
        }
    }
}
