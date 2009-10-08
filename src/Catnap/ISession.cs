using System;
using System.Collections.Generic;
using Catnap.Common;
using Catnap.Maps;

namespace Catnap
{
    public interface ISession : IDisposable
    {
        void Open();
        IDbCommand CreateCommand(DbCommandSpec commandSpec);
        IList<IDictionary<string, object>> List(DbCommandSpec commandSpec);
        IList<T> List<T>(DbCommandSpec commandSpec) where T : class, IEntity, new();
        T Get<T>(int id) where T : class, IEntity, new();
        void Save<T>(T entity) where T : class, IEntity, new();
        void Save<T>(T entity, int? parentId) where T : class, IEntity, new();
        void Delete<T>(int id) where T : class, IEntity, new();
        void ExecuteNonQuery(DbCommandSpec commandSpec);
        IEnumerable<IDictionary<string, object>> ExecuteQuery(DbCommandSpec commandSpec);
        void RollbackTransaction();
        T BuildFrom<T>(IEntityMap<T> entityMap, IDictionary<string, object> record) where T : class, IEntity, new();
    }
}