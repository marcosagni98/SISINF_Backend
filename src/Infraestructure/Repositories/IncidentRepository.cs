﻿using Domain.Entities;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infraestructure.Repositories;

public class IncidentRepository : IIncidentRepository
{
    private readonly AppDbContext _context;
    private DbSet<Incident> _dbSet;

    public IncidentRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<Incident>();
    }

    #region Dispose
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {

            _context?.Dispose();
        }
    }
    #endregion
}

