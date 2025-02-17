using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ProductCatalog.Api.Models;

public record CategoryCreate(Guid? ParentId, string Name, string Description);
public record Category(Guid Id, Guid? ParentId, string Name, string Description) : CategoryCreate(ParentId, Name, Description);
