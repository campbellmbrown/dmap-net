using System;

namespace DMap.Models;

public sealed record MapSession(Guid SessionId, int MapWidth, int MapHeight);
