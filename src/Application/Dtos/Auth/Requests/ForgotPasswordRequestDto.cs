﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Dtos.Auth.Requests;

/// <summary>
/// Recover password request
/// </summary>
public class ForgotPasswordRequestDto
{
    /// <summary>
    /// User email
    /// </summary>
    public string Email { get; set; } = string.Empty;
}