using System.Runtime.CompilerServices;

// Permite que repositórios de provider acessem hooks protected internal
// (OnBeforeSave, etc.) sem expor publicamente.
[assembly: InternalsVisibleTo("LightOrm.Mongo")]
[assembly: InternalsVisibleTo("LightOrm.MySql")]
[assembly: InternalsVisibleTo("LightOrm.Sqlite")]
[assembly: InternalsVisibleTo("LightOrm.Postgres")]
[assembly: InternalsVisibleTo("LightOrm.Core.Tests")]
