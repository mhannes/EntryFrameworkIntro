using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

internal class Program
{
    private static async Task Main(string[] args)
    {
        int i = null;
        string s = null;

        var factory = new CookbookContextFactory();
        using var dbContext = factory.CreateDbContext(args);

        var newDish = new Dish { Title = "Foo", Notes = "Bar" };
        dbContext.Dishes.Add(newDish);
        await dbContext.SaveChangesAsync();

        newDish.Notes = "Baz";
        await dbContext.SaveChangesAsync();

        await EntityStates(factory, args);
        await ChangeTracking(factory, args);
        await AttachEntities(factory, args);
        await NoTracking(factory, args);
        await RawSql(factory, args);
        await Transactions(factory, args);
        await ExpressionTree(factory, args);

        //Expression Tree
        static async Task ExpressionTree(CookbookContextFactory factory, string[] args)
        {
            using var dbContext = factory.CreateDbContext(args);
            var newDish = new Dish { Title = "Foo", Notes = "Bar" };
            dbContext.Dishes.Add(newDish);
            await dbContext.SaveChangesAsync();

            var dishes = await dbContext.Dishes
                .Where(d => d.Title.StartsWith("F"))
                .ToArrayAsync();



        }

        // Using Transactions (Rollback of the changes that where made during a transaction)
        static async Task Transactions(CookbookContextFactory factory, string[] args)
        {
            using var dbContext = factory.CreateDbContext(args);
            using var transaction = await dbContext.Database.BeginTransactionAsync();

            try
            {
                dbContext.Dishes.Add(new Dish { Title = "Foo", Notes = "Bar" });
                await dbContext.SaveChangesAsync();

                await dbContext.Database.ExecuteSqlRawAsync("SELECT 1/0 as Bad");
                await transaction.CommitAsync();
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"Something bad happened: {ex.Message}");
            }

        }

        // Use of RawSQL
        static async Task RawSql(CookbookContextFactory factory, string[] args)
        {
            using var dbContext = factory.CreateDbContext(args);
            var dishes = await dbContext.Dishes
                .FromSqlRaw("SELECT * FROM Dishes")
                .ToArrayAsync();

            var filter = "%z";
            dishes = await dbContext.Dishes
                .FromSqlInterpolated($"SELECT * FROM Dishes WHERE Notes LIKE {filter}")
                //.AsNoTracking()
                .ToArrayAsync();

            // BAD BAD BAD BAD BAD BAD BAD BAD BAD BAD BAD BAD
            // BAD BAD BAD BAD BAD BAD BAD BAD BAD BAD BAD BAD
            // BAD BAD BAD BAD BAD BAD BAD BAD BAD BAD BAD BAD
            // SQL INJECTION (var filter = "%Z; DROP TABLE DISHES)
            //dishes = await dbContext.Dishes
            //    .FromSqlRaw("SELECT * FROM Dishes WHERE Notes LIKE '" + filter + "'")
            //    .ToArrayAsync();

            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Dishes WHERE Id NOT IN (SELECT DishId FROM Ingredients)");

        }

        // Entity States
        static async Task EntityStates(CookbookContextFactory factory, string[] args)
        {
            using var dbContext = factory.CreateDbContext(args);
            var newDish = new Dish { Title = "Foo", Notes = "Bar" };
            var state = dbContext.Entry(newDish).State; // -->Detached

            dbContext.Dishes.Add(newDish);
            state = dbContext.Entry(newDish).State; // -->Added

            await dbContext.SaveChangesAsync();
            state = dbContext.Entry(newDish).State; // -->Unchanged

            newDish.Notes = "Baz";
            state = dbContext.Entry(newDish).State; // -->Modified
            await dbContext.SaveChangesAsync();

            dbContext.Dishes.Remove(newDish);
            state = dbContext.Entry(newDish).State; // -->Deleted
            await dbContext.SaveChangesAsync();

            state = dbContext.Entry(newDish).State; // -->Detached
        }

        // Change Tracking Systems
        static async Task ChangeTracking(CookbookContextFactory factory, string[] args)
        {
            using var dbContext = factory.CreateDbContext(args);
            var newDish = new Dish { Title = "Foo", Notes = "Bar" };
            dbContext.Dishes.Add(newDish);
            await dbContext.SaveChangesAsync();
            newDish.Notes = "Baz";

            var entry = dbContext.Entry(newDish);
            var originalValue = entry.OriginalValues[nameof(Dish.Notes)].ToString();
            var dishFromDatabase = await dbContext.Dishes.SingleAsync(d => d.Id == newDish.Id);


            // -------
            using var dbContext2 = factory.CreateDbContext(args);
            var dishFromDatabase2 = await dbContext2.Dishes.SingleAsync(d => d.Id == newDish.Id);
        }

        //Attaching Entities
        static async Task AttachEntities(CookbookContextFactory factory, string[] args)
        {
            using var dbContext = factory.CreateDbContext(args);
            var newDish = new Dish { Title = "Foo", Notes = "Bar" };
            dbContext.Dishes.Add(newDish);
            await dbContext.SaveChangesAsync();

            //EF: forget the "newDish" object
            dbContext.Entry(newDish).State = EntityState.Detached;
            var state = dbContext.Entry(newDish).State;

            //EF updates the whole object "newDish"
            dbContext.Dishes.Update(newDish);
            await dbContext.SaveChangesAsync();

        }

        //No Tracking --> increasing performance
        static async Task NoTracking(CookbookContextFactory factory, string[] args)
        {
            using var dbContext = factory.CreateDbContext(args);
            //Select * from dishes
            var dishes = await dbContext.Dishes.AsNoTracking().ToArrayAsync();
            var state = dbContext.Entry(dishes[0]).State;


        }



        //Console.WriteLine("Add Porridge for breakfast");
        //var Porridge = new Dish { Title = "Breakfast Porridge", Notes = "This is sooooo gooood", Stars = 4 };
        //context.Dishes.Add(Porridge);
        //await context.SaveChangesAsync();
        //Console.WriteLine($"Added Porridge (Id: {Porridge.Id}) successfully");

        //Console.WriteLine("Checking stars for Porridge");
        //var dishes = await context.Dishes
        //    .Where(d => d.Title.Contains("Porridge")) //LINQ -> SQL
        //    .ToListAsync();
        //if (dishes.Count != 1) Console.Error.WriteLine("Something really bad happened. Porridge disappeared!");
        //Console.WriteLine($"Porride has {dishes[0].Stars} stars");

        //Console.WriteLine("Change Porridge stars to 5");
        //Porridge.Stars = 5;
        //await context.SaveChangesAsync();
        //Console.WriteLine("Changed Porridge stars successfully");

        //Console.WriteLine("Removing Porridge from database");
        //context.Dishes.Remove(Porridge);
        //await context.SaveChangesAsync();
        //Console.WriteLine($"Porridge removed successfully");



        Console.Write("Press any key to end the program.");
        Console.ReadKey();
    }
}


//create the model class
class Dish
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public int? Stars { get; set; }

    public List<DishIngredient> Ingredients { get; set; } = new();
}

class DishIngredient
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(50)]
    public string UnitOfMeasure { get; set; } = string.Empty;

    [Column(TypeName = "decimal(5,2)")]
    public decimal Amount { get; set; }

    public Dish? Dish { get; set; }

    public int DishId { get; set; }
}

class CookbookContext : DbContext
{
    public DbSet<Dish> Dishes => Set<Dish>();

    public DbSet<DishIngredient> Ingredients { get; set; }

    public CookbookContext(DbContextOptions<CookbookContext> options)
        : base(options)
    {

    }
}

class CookbookContextFactory : IDesignTimeDbContextFactory<CookbookContext>
{
    public CookbookContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        var optionsBuilder = new DbContextOptionsBuilder<CookbookContext>();
        optionsBuilder
            // Uncomment the following line if you want to print generated
            // SQL statements on the console.
            .UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
            .UseSqlServer(configuration["ConnectionStrings:DefaultConnection"]);

        return new CookbookContext(optionsBuilder.Options);
    }
}