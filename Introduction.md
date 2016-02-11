# Features #

  * Uses ADO.NET API.  Currently implemented for Sqlite.  Tested with:
    * System.Data.Sqlite
    * Mono.Data.Sqlite
  * Simple fluent mapping
    * Entities
    * Many to one associations (belongs to)
    * One to many associations (collections):
      * Loading - automatically load collections, lazily or eagerly, as specified per collection
      * Cascading - automatically save/update/delete changes to collections, as specified per collection
      * Filtering - selectively load items base on criteria specified per collection
  * Connection and transaction boundaries controlled via a thread safe Unit Of Work context
  * Repository`<T>` base class implements basic persistence operations
  * Simple ICriteria API to build complex find queries
  * Create any kind of parameterized raw SQL query in code
  * Use Linq predicates for queries and to filter collections (very limited at present)
  * Database migration utility
    * Automatically deploy database changes based on migrations that you write (forward only)
    * Very useful for embedded database scenarios
  * Very lightweight (currently about 50k)

# Configuration #

Startup might look something like this:

```
SessionFactory.Initialize("Data source=MyDatabase.sqlite", new SqliteAdapter());
Domain.Configure
(
    Map.Entity<Person>()
        .Table("Users")
        .Property(x => x.FirstName)
        .Property(x => x.LastName),
    Map.Entity<Forum>()
        .Property(x => x.Name),
        .List(x => x.Posts)
    Map.Entity<Post>()
        .ParentColumn("ForumId")
        .Property(x => x.Title)
        .Property(x => x.Body)
        .BelongsTo(x => x.Poster, "PosterId")
);
using (UnitOfWork.Start())
{
    DatabaseMigrator.Execute();
}
```

Notice that you do not have to map the Id property.  All other persisted properties must be mapped and must have both a getter and a setter.  The getters and setters _do not_ have to be public.

## MonoTouch ##

There are some special considerations for [using Catnap with MonoTouch](UsingCatnapWithMonoTouch.md).

# IEntity Interface #

Mapped entities must implement the IEntity interface.  All entities use an integer primary key which is assumed to be auto-incremented by the database.

```
public interface IEntity
{
    int Id { get; }
    bool IsTransient { get; }
    void SetId(int id);
}
```

Catnap also provides a base class, Entity, which implements IEntity and provides equality comparison.  This is for convenience and is not required.

# Usage #

## Get and Save ##

Below we fetch a forum, change its title, remove a post and add a post, then save the forum.  The Posts collection will be loaded lazily.  The adds/deletes to Posts will be cascaded when the forum is saved.  At the start of the using block a connection will be opened and a transaction started.  At the end of the using block the transaction will be committed (or rolled back in the case of an error) and the connection will be closed.

```
using (UnitOfWork.Start())
{
   var person = personRepository.Get(1);
   var forum = forumRepository.Get(1);
   forum.Title = "Annoying Complaints";
   forum.RemovePost(forum.Posts[0]);
   forum.AddPost(new Post { Title = "Please help!", Body "Now!", Person = person });
   forumRepository.Save(forum);
}
```

## Repository`<T>` ##

Catnap provides a base repository implementation with the basic persistence operations.  The base repository implements this interface:

```
public interface IRepository<T> where T : class, IEntity, new()
{
    T Get(int id);
    void Save(T entity);
    void Delete(int id);
    IEnumerable<T> Find();
    IEnumerable<T> Find(Expression<Func<T, bool>> predicate);
    IEnumerable<T> Find(ICriteria criteria);
}
```

## Criteria ##

Criteria provides a way to specify conditions for a find command.  For example:

```
public class TimeEntryRepository : Repository<TimeEntry>, ITimeEntryRepository
{
    public IEnumerable<TimeEntry> Find(int projectId)
    {
        return Find(new TimeEntriesCriteria().Project(projectId));
    }

    public IEnumerable<TimeEntry> FindUnbilled(int projectId)
    {
        return Find(new TimeEntriesCriteria().Project(projectId).Billed(false));
    }

    public IEnumerable<TimeEntry> FindBilled(int projectId)
    {
        return Find(new TimeEntriesCriteria().Project(projectId).Billed(true));
    }

    private class TimeEntriesCriteria : Criteria
    {
        public TimeEntriesCriteria Project(int projectId)
        {
            Add(Condition.Equal<TimeEntry>(x => x.Project, projectId));
            return this;
        }

        public TimeEntriesCriteria Billed(bool billed)
        {
            Add(Condition.Equal<TimeEntry>(x => x.Billed, billed));
            return this;
        }
    }
}
```

The following unit test illustrates more complex criteria:

```
public class when_creating_a_complex_condition
{
    static Criteria target;

    Because of = () =>
    {
        DomainMap.Configure(new EntityMap<Person>().Property(x => x.FirstName));
        target = new Criteria
        (
            Condition.Less("Bar", 1000),
            Condition.GreaterOrEqual("Bar", 300),
            Condition.Or
            (
                Condition.NotEqual<Person>(x => x.FirstName, "Tim"),
                Condition.And
                (
                    Condition.Equal("Foo", 25),
                    Condition.Equal("Baz", 500)
                )
            )
        );
    };

    It should_render_correct_string = () => target.Build().ToString().Should()
        .Equal("((Bar < @0) and (Bar >= @1) and ((FirstName != @2) or ((Foo = @3) and (Baz = @4))))");

    It should_contain_expected_parameters = () =>
    {
        target.Build();
        target.Parameters.Should().Count.Exactly(5);
        target.Parameters.Should().Contain.One(x => x.Name == "@0" && x.Value.Equals(1000));
        target.Parameters.Should().Contain.One(x => x.Name == "@1" && x.Value.Equals(300));
        target.Parameters.Should().Contain.One(x => x.Name == "@2" && x.Value.Equals("Tim"));
        target.Parameters.Should().Contain.One(x => x.Name == "@3" && x.Value.Equals(25));
        target.Parameters.Should().Contain.One(x => x.Name == "@4" && x.Value.Equals(500));
    };
}
```

One notable limitation of the criteria API is that it only provides for conditions on simple properties of the entity.  It does not provide for any aggregate operations on collection or tests against nested properties.

### DbCommandSpec ###

Where Criteria leaves off, DbCommandSpec picks up.  It allows you to do more complex queries and still return entities instead of raw data.  For example:

```
var command = new DbCommandSpec()
    .SetCommandText(
        @"select e.* from TimeEntry e 
            inner join Project p on p.Id = e.ProjectId
            inner join Client c on c.Id = p.ClientId
          where c.Id = @clientId")
    .AddParameter("@clientId", clientId);
return UnitOfWork.Current.Session.List<TimeEntry>(command);
```

This will return a list of TimeEntry items.

### ExecuteQuery ###

In cases where you want to fetch raw data, perhaps to populate DTOs, you can use
ExecuteQuery.  For example:

```
var command = new DbCommandSpec()
    .SetCommandText(
        @"select e.Id, c.Name from TimeEntry e
            inner join Project p on e.ProjectId = p.Id
            inner join Client c on p.ClientId = c.Id
          where c.Name = @name")
   .AddParameter("@name", "Acme")
return UnitOfWork.Current.Session.ExecuteQuery(command);
```

The return value is of type `IEnumerable<IDictionary<string, object>>`, each item of which is a row.  Each item in the dictionary is a cell with Key being the cell name and Value being the cell value.

### ExecuteNonQuery ###

For example:

```
var command = new DbCommandSpec()
    .SetCommandText("create table Foo (Bar varchar(200))");
UnitOfWork.Current.Session.ExecuteNonQuery(command);
```

### ExecuteScalar ###

For example:

```
var command = new DbCommandSpec()
    .SetCommandText("select count(*) from Foo where Bar = 'bar'");
int count = UnitOfWork.Current.Session.ExecuteScalar<int>(command);
```

# Development Priorities #

  * Automated tests.  Catnap started as an experimental project that has evolved into something useful.  While it was developed with loose coupling and testability in mind, TDD/BDD was not used.  Getting the code under test will be necessary to allow for fluid change and growth.
    * Unit tests, lots of unit tests.
    * More integration tests.
  * Session cache. Currently there is no caching of fetched entities.  This creates a performance problem for cascading.  To cascade we must retrieve the entire persisted collection in order to synchronize changes.  Therefore, cascading is currently only appropriate for collections of a small size.
  * Lazy loading of BelongsTo (many-to-one) properties.
  * Introduce lightweight IoC, if possible.  (Most IoC containers won't work on MonoTouch.  Posibilities are [Funq](http://funq.codeplex.com/) and [PicoContainer.NET](http://docs.codehaus.org/display/PICO/Home))
  * Create adapters for other ADO.NET providers.  This might require some work to the core code to support differences in SQL dialect.