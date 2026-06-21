using Artificer.Plugin;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace Artificer.Application.CraftingLists;

/// <summary>Abre o mapa do jogo com uma flag no nó de coleta. No-op se não houver coords.</summary>
public static class MapFlagHelper
{
    public static void FlagOnMap(GatheringLocation loc)
    {
        if (loc.MapX <= 0f || loc.MapId == 0)
            return;
        Service.GameGui.OpenMapWithMapLink(
            new MapLinkPayload(loc.TerritoryTypeId, loc.MapId, loc.MapX, loc.MapY));
    }
}
