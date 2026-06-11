using Artificer.Plugin;
using System;
using System.Collections.Generic;

namespace Artificer.Utils;

/// <summary>Abstraction over macro persistence; implemented by <see cref="MacroRepository"/>.</summary>
public interface IMacroStore
{
    IReadOnlyList<Macro> Macros { get; }

    /// <summary>Fired after <see cref="Update"/> persists a macro mutation.</summary>
    event Action<Macro>? MacroUpdated;

    /// <summary>Fired when the macro list itself changes (add, remove, reorder).</summary>
    event Action? MacroListChanged;

    void Add(Macro macro);
    void Remove(Macro macro);
    void Swap(int fromIdx, int toIdx);
    void Move(int fromIdx, int toIdx);
    void Update(Macro macro);
}
