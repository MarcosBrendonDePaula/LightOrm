using System;
using LightOrm.Core.Attributes;
using LightOrm.MySql;
using LightOrm.Postgres;
using LightOrm.Sqlite;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using Npgsql;

namespace LightOrm.Core.Tests
{
    public class DialectTests
    {
        [Fact]
        public void SqliteDialect_maps_and_converts_core_types()
        {
            var dialect = new SqliteDialect();

            Assert.Equal("INTEGER NOT NULL", dialect.MapType(typeof(int), new ColumnAttribute("id")));
            Assert.Equal("NUMERIC", dialect.MapType(typeof(decimal?), new ColumnAttribute("amount")));
            Assert.Equal(1L, dialect.ToDbValue(true, typeof(bool)));
            Assert.Equal(0L, dialect.ToDbValue(false, typeof(bool)));
            Assert.Equal("12345678-1234-1234-1234-123456789abc", dialect.ToDbValue(Guid.Parse("12345678-1234-1234-1234-123456789abc"), typeof(Guid)));
            Assert.True((bool)dialect.FromDbValue(1L, typeof(bool)));
            Assert.Equal(12.5m, dialect.FromDbValue(12.5d, typeof(decimal)));
            Assert.Equal(7m, dialect.FromDbValue(7L, typeof(decimal)));
        }

        [Fact]
        public void SqliteDialect_rejects_wrong_connection_type()
        {
            var dialect = new SqliteDialect();
            using var conn = new MySqlConnection();

            Assert.Throws<ArgumentException>(() => dialect.CreateCommand(conn));
        }

        [Fact]
        public void MySqlDialect_maps_unsigned_and_converts_guid()
        {
            var dialect = new MySqlDialect();
            var unsigned = new ColumnAttribute("counter", isUnsigned: true);

            Assert.Equal("INT UNSIGNED NOT NULL", dialect.MapType(typeof(int), unsigned));
            Assert.Equal("VARCHAR(255) NOT NULL", dialect.MapType(typeof(string), new ColumnAttribute("name")));
            Assert.Equal("12345678-1234-1234-1234-123456789abc", dialect.ToDbValue(Guid.Parse("12345678-1234-1234-1234-123456789abc"), typeof(Guid)));
            Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789abc"), dialect.FromDbValue("12345678-1234-1234-1234-123456789abc", typeof(Guid)));
        }

        [Fact]
        public void MySqlDialect_rejects_wrong_connection_type()
        {
            var dialect = new MySqlDialect();
            using var conn = new SqliteConnection();

            Assert.Throws<ArgumentException>(() => dialect.CreateCommand(conn));
        }

        [Fact]
        public void PostgresDialect_maps_serial_and_converts_special_types()
        {
            var dialect = new PostgresDialect();

            Assert.Equal("SERIAL", dialect.MapType(typeof(int), new ColumnAttribute("id", isPrimaryKey: true, autoIncrement: true)));
            Assert.Equal("BIGSERIAL", dialect.MapType(typeof(long), new ColumnAttribute("id", isPrimaryKey: true, autoIncrement: true)));
            Assert.Equal("UUID NOT NULL", dialect.MapType(typeof(Guid), new ColumnAttribute("external_id")));
            Assert.Equal((byte)7, dialect.FromDbValue(7, typeof(byte)));
            Assert.Equal(TestStatus.Active, dialect.FromDbValue(1, typeof(TestStatus)));
            Assert.Equal(Guid.Parse("12345678-1234-1234-1234-123456789abc"), dialect.FromDbValue("12345678-1234-1234-1234-123456789abc", typeof(Guid)));
        }

        [Fact]
        public void PostgresDialect_rejects_invalid_autoincrement_type_and_wrong_connection_type()
        {
            var dialect = new PostgresDialect();
            using var conn = new SqliteConnection();

            Assert.Throws<NotSupportedException>(() =>
                dialect.MapType(typeof(Guid), new ColumnAttribute("id", isPrimaryKey: true, autoIncrement: true)));
            Assert.Throws<ArgumentException>(() => dialect.CreateCommand(conn));
        }

        private enum TestStatus
        {
            Pending = 0,
            Active = 1
        }
    }
}
