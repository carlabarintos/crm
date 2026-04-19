using CrmSales.Contacts.Domain.Entities;
using CrmSales.Contacts.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CrmSales.Api.Endpoints;

public static class ContactEndpoints
{
    public static IEndpointRouteBuilder MapContactEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/contacts")
            .WithTags("Contacts")
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] string? search,
            IContactRepository repo,
            CancellationToken ct) =>
        {
            var contacts = await repo.SearchAsync(search, ct);
            return Results.Ok(contacts.Select(c => new
            {
                c.Id, c.FirstName, c.LastName, c.FullName,
                c.Email, c.Phone, c.Company, c.JobTitle,
                c.IsActive, c.CreatedAt
            }));
        });

        group.MapGet("/{id:guid}", async (Guid id, IContactRepository repo, CancellationToken ct) =>
        {
            var contact = await repo.GetByIdAsync(id, ct);
            return contact is null ? Results.NotFound() : Results.Ok(new
            {
                contact.Id, contact.FirstName, contact.LastName, contact.FullName,
                contact.Email, contact.Phone, contact.Company, contact.JobTitle,
                contact.Notes, contact.IsActive, contact.CreatedAt, contact.UpdatedAt
            });
        });

        group.MapPost("/", async (CreateContactRequest req, IContactRepository repo, CancellationToken ct) =>
        {
            var contact = Contact.Create(
                req.FirstName, req.LastName,
                req.Email, req.Phone,
                req.Company, req.JobTitle, req.Notes);
            await repo.AddAsync(contact, ct);
            return Results.Created($"/api/contacts/{contact.Id}", new
            {
                contact.Id, contact.FullName, contact.Email, contact.Company
            });
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] CreateContactRequest req,
            IContactRepository repo,
            CancellationToken ct) =>
        {
            var contact = await repo.GetByIdAsync(id, ct);
            if (contact is null) return Results.NotFound();
            contact.Update(req.FirstName, req.LastName, req.Email, req.Phone,
                req.Company, req.JobTitle, req.Notes);
            await repo.UpdateAsync(contact, ct);
            return Results.Ok(new { contact.Id, contact.FullName });
        });

        group.MapDelete("/{id:guid}", async (Guid id, IContactRepository repo, CancellationToken ct) =>
        {
            var contact = await repo.GetByIdAsync(id, ct);
            if (contact is null) return Results.NotFound();
            await repo.DeleteAsync(contact, ct);
            return Results.NoContent();
        });

        return app;
    }
}

record CreateContactRequest(
    string FirstName, string LastName,
    string? Email, string? Phone,
    string? Company, string? JobTitle, string? Notes);
