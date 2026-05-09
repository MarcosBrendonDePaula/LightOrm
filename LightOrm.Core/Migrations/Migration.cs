using System.Threading.Tasks;

namespace LightOrm.Core.Migrations
{
    /// <summary>
    /// Base abstrata para migrations. Implemente Up para aplicar e Down para
    /// reverter. Nome default = nome da classe; ordene por prefixo timestamp
    /// (ex.: M20260509_120000_CreateUsersTable).
    ///
    /// Use as versões Async se a migration precisa de Task assíncrona
    /// (ex.: data migration via repositório). As versões síncronas cobrem
    /// só DDL via SchemaBuilder, que é puro montar SQL — o execute é Async.
    /// </summary>
    public abstract class Migration
    {
        public virtual string Name => GetType().Name;

        public abstract void Up(SchemaBuilder schema);
        public abstract void Down(SchemaBuilder schema);

        // Override quando a migration precisa rodar lógica além do SchemaBuilder
        // (ex.: backfill de dados). Default: chama Up/Down síncrono.
        public virtual Task UpAsync(SchemaBuilder schema)
        {
            Up(schema);
            return Task.CompletedTask;
        }

        public virtual Task DownAsync(SchemaBuilder schema)
        {
            Down(schema);
            return Task.CompletedTask;
        }
    }
}
