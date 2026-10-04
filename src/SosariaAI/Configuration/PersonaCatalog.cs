using System;
using System.Collections.Generic;

namespace SosariaAI.Configuration;

public sealed class PersonaCatalog
{
    private readonly Dictionary<string, Persona> _byId;
    private readonly Persona _neutral = Persona.CreateNeutral();

    public PersonaCatalog(Dictionary<string, Persona> byId) =>
        _byId = byId ?? new Dictionary<string, Persona>(StringComparer.OrdinalIgnoreCase);

    public Persona Neutral => _neutral;

    public IReadOnlyCollection<Persona> All => _byId.Values;

    public Persona Resolve(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return _neutral;
        }

        return _byId.TryGetValue(id, out var persona) ? persona : _neutral;
    }
}
