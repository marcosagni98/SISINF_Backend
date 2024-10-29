﻿using Application.Dtos.CRUD.Incidents;
using Application.Dtos.CRUD.Incidents.Request;

namespace Application.Interfaces;

/// <summary>
/// incident Service Interface
/// </summary>
public interface IIncidentService : IBaseService<IncidentDto, IncidentAddRequestDto, IncidentUpdateRequestDto>, IDisposable
{

}
