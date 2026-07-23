using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.AuthService.Tests;

public class TestDevHuntDbContext(DbContextOptions<DevHuntDbContext> options) : DevHuntDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Project>().Ignore(p => p.OpenRoles);
    }
}
