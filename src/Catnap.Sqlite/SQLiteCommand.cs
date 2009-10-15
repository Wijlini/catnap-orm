using System;
using System.Collections.Generic;
using System.Linq;
using Catnap.Common.Database;
using Catnap.Common.Logging;

namespace Catnap.Sqlite
{
    public class SqliteCommand : IDbCommand
    {
        private IntPtr database;
        private readonly DbCommandSpec commandSpec;
        private SqliteTypeConverter typeConverter = new SqliteTypeConverter();

        internal SqliteCommand(IntPtr database, DbCommandSpec commandSpec)
        {
            this.database = database;
            this.commandSpec = commandSpec;
            ConvertParameters();
        }

        private void ConvertParameters()
        {
            foreach(var parameter in commandSpec.Parameters)
            {
                parameter.Value = typeConverter.ConvertToDbType(parameter.Value);
            }
        }

        public int ExecuteNonQuery()
        {
            var statement = Prepare();
            var result = Sqlite3.Step(statement);
            switch (result)
            {
                case SqliteResult.Done:
                    var rowsAffected = Sqlite3.Changes(database);
                    Log.Debug(string.Format("{0} rows affected", rowsAffected));
                    return rowsAffected;
                default:
                    throw new SqliteException(Sqlite3.Errmsg(database));
            }
        }

        public IEnumerable<IDictionary<string, object>> ExecuteQuery()
        {
            var statement = Prepare();
            var count = 0;
            while (Sqlite3.Step(statement) == SqliteResult.Row)
            {
                var row = new Dictionary<string, object>();
                var columnCount = Sqlite3.ColumnCount(statement);
                for (var i = 0; i < columnCount; i++)
                {
                    row.Add(Sqlite3.ColumnName(statement, i), ReadColumn(statement, i));
                }
                yield return row;
                count++;
            }
            Log.Debug("Returning {0} rows", count);
        }

        public T ExecuteScalar<T>()
        {
            var result = ExecuteQuery();
            if (result.Count() > 1)
            {
                throw new SqliteException(string.Format("Expected one row. Got {0}.", result.Count()));
            }
            if (result.Count() == 0)
            {
                return default(T);
            }
            var value = result.ToList()[0].ToList()[0].Value;
            if (value == null)
            {
                return default(T);
            }
            if (value is T)
            {
                return (T) value;  
            }
            throw new SqliteException(string.Format("The value returned is not of the expected type. Expected: {0}.  Actual: {1}", typeof(T).Name, value.GetType()));
        }

        public override string ToString()
        {
            return commandSpec.ToString();
        }

        private IntPtr Prepare()
        {
            var parameters = commandSpec.Parameters.Select(x => string.Format("{0}={1}", x.Name, x.Value)).ToArray();
            Log.Debug("Preparing query: {0}{1}. ", commandSpec,  parameters.Count() == 0
                ? string.Empty
                : string.Format(" [PARAMETERS: {0}]", string.Join(", ", parameters)));

            var statement = Sqlite3.Prepare(database, commandSpec.ToString());
            BindAll(statement);
            return statement;
        }

        private void BindAll(IntPtr statement)
        {
            var nextIdx = 1;
            foreach (var b in commandSpec.Parameters)
            {
                b.Index = b.Name != null
                    ? Sqlite3.BindParameterIndex(statement, b.Name)
                    : nextIdx++;
            }
            foreach (var parameter in commandSpec.Parameters)
            {
                if (parameter.Value == null)
                {
                    Sqlite3.BindNull(statement, parameter.Index);
                }
                else
                {
                    if (parameter.Value is Byte || parameter.Value is UInt16 || parameter.Value is SByte || parameter.Value is Int16 || parameter.Value is Int32)
                    {
                        Sqlite3.BindInt(statement, parameter.Index, Convert.ToInt32(parameter.Value));
                    }
                    else if (parameter.Value is UInt32 || parameter.Value is Int64)
                    {
                        Sqlite3.BindInt64(statement, parameter.Index, Convert.ToInt64(parameter.Value));
                    }
                    else if (parameter.Value is Single || parameter.Value is Double || parameter.Value is Decimal)
                    {
                        Sqlite3.BindDouble(statement, parameter.Index, Convert.ToDouble(parameter.Value));
                    }
                    else if (parameter.Value is String)
                    {
                        Sqlite3.BindText(statement, parameter.Index, parameter.Value.ToString(), -1, new IntPtr(-1));
                    }
                    else if (parameter.Value is bool)
                    {
                        Sqlite3.BindInt(statement, parameter.Index, (bool)parameter.Value ? 1 : 0);
                    }
                    else if (parameter.Value is DateTime)
                    {
                        Sqlite3.BindText(statement, parameter.Index, parameter.Value.ToString(), -1, new IntPtr(-1));
                    }
                }
            }
        }

        private object ReadColumn(IntPtr statement, int index)
        {
            var columnType = Sqlite3.ColumnType(statement, index);
            switch (columnType)
            {
                case SqliteColumnType.Null:
                    return null;
                case SqliteColumnType.Text:
                    return Sqlite3.ColumnText(statement, index);
                case SqliteColumnType.Integer:
                    var val = Sqlite3.ColumnInt64(statement, index);
                    if (val >= int.MinValue && val <= int.MaxValue)
                    {
                        return (int) val;
                    }
                    return val;
                case SqliteColumnType.Float:
                    return Sqlite3.ColumnDouble(statement, index);
                case SqliteColumnType.Blob:
                    return Sqlite3.ColumnBlob(statement, index);
                default:
                    throw new NotSupportedException("Don't know how to read " + columnType);
            }
        }
    }
}