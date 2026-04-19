using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Contacts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Contacts.Infrastructure.Persistence;

public sealed class ContactsDbContext(DbContextOptions<ContactsDbContext> options)
    : DbContext(options)
{
    public DbSet<Contact> Contacts => Set<Contact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContactsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
