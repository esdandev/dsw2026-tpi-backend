using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Data.Identity;

public class RevokedToken
{
    public string Jti { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime RevokedAtUtc { get; set; }
}
