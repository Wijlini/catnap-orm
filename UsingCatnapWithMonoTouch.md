# Using Catnap With MonoTouch #

## Alternate Mapping Syntax ##

Because of a [bug in MonoTouch](http://bugzilla.novell.com/show_bug.cgi?id=551623), the mapping syntax shown in the configuration example in the [Introduction](Introduction.md) does not work on iOS devices.  Fortunately, there is a workaround.  It's a little more verbose, but it works.  The following example is functionally equivalent to the example in the [Introduction](Introduction.md).

```
Domain.Configure
(
    Map.Entity<Person>()
        .Table("Users")
        .Map(new ValuePropertyMap<Person, string>(x => x.FirstName))
        .Map(new ValuePropertyMap<Person, string>(x => x.LastName)),
    Map.Entity<Forum>()
        .Map(new ValuePropertyMap<Forum, string>(x => x.Name)),
        .Map(new ListPropertyMap<Forum, Post>(x => x.Posts, true))
    Map.Entity<Post>()
        .ParentColumn("ForumId")
        .Map(new ValuePropertyMap<Post, string>(x => x.Title))
        .Map(new ValuePropertyMap<Post, string>(x => x.Body))
        .Map(new BelongsToPropertyMap<Post, Person>(x => x.Poster, "PosterId"))
);
```

## Mono.Data.Sqlite ##

In the configuration example in the [Introduction](Introduction.md), we pass a SqliteAdapter into the SessionFactory.  Among other things, this tells Catnap to find System.Data.Sqlite.dll in the bin folder.  If you are using Mono.Data.Sqlite.dll you must tell the adapter the type of the  connection, like so:

```
SessionFactory.Initialize("Data source=MyDatabase.sqlite", 
    new SqliteAdapter(typeof(Mono.Data.Sqlite.SqliteConnection));
```

Couldn't we just add MonoSqliteAdapter to Catnap?  We could but it won't work on iOS devices.  MonoTouch, it seems, is not quite able to dynamically load a type at runtime.  It throws a TypeLoadException.