using System.Linq;
using Catnap.Common.Database;
using Catnap.Sqlite;

namespace Catnap.Migration
{
    public class DatabaseMigratorUtility
    {
        private const string MIGRATIONS_TABLE_NAME = "db_migrations";
        private readonly IMetadataCommandFactory metadataCommandFactory;

        public DatabaseMigratorUtility(IMetadataCommandFactory metadataCommandFactory)
        {
            this.metadataCommandFactory = metadataCommandFactory;
        }

        public void Migrate(params IDatabaseMigration[] migrations)
        {
            CreateMigrationsTableIfNotExists();
            foreach (var migration in migrations)
            {
                if (!PreviouslyRun(migration))
                {
                    migration.Action();
                    RecordMigration(migration);
                }
            }
        }

        private void RecordMigration(IDatabaseMigration migration)
        {
            var migrationsTableExistsCommand = new DbCommandSpec()
                .SetCommandText(string.Format("insert into {0} (Name) values (@name)", MIGRATIONS_TABLE_NAME))
                .AddParameter("@name", migration.Name);
            UnitOfWork.Current.Session.ExecuteNonQuery(migrationsTableExistsCommand);
        }

        private bool PreviouslyRun(IDatabaseMigration migration)
        {
            var command = new DbCommandSpec()
                .SetCommandText(string.Format(@"select * from {0} where Name = @name", MIGRATIONS_TABLE_NAME))
                .AddParameter(migration.Name);
            var result = UnitOfWork.Current.Session.ExecuteQuery(command);
            return result.Count() > 0;
        }

        private void CreateMigrationsTableIfNotExists()
        {
            var existsResult = UnitOfWork.Current.Session.ExecuteQuery(
                metadataCommandFactory.GetGetTableMetadataCommand(MIGRATIONS_TABLE_NAME));
            if (existsResult.Count() == 0)
            {
                var createMigrationsTable = new DbCommandSpec()
                    .SetCommandText(string.Format("create table {0} (Name varchar(200))", MIGRATIONS_TABLE_NAME));
                UnitOfWork.Current.Session.ExecuteNonQuery(createMigrationsTable);
            }
        }
    }
}