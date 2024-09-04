using DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Data.Entities;

namespace DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Data;

using Microsoft.EntityFrameworkCore;

public class PersonDbContext(DbContextOptions<PersonDbContext> options) : DbContext(options)
{
    public DbSet<Person> People { get; set; }
}
