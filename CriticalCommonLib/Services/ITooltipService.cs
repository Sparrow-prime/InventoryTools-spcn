using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace CriticalCommonLib.Services;

public interface ITooltipService : IDisposable
{
    void AddTooltipTweak(TooltipService.TooltipTweak tooltipTweak);
    unsafe bool ItemHoveredDetour(AgentItemDetail* agentItemDetail, InventoryItem** inventoryItem, InventoryType* inventoryType, short* a4, uint a5, uint a6, InventoryItem* a7);
    void Disable();
    unsafe void GenerateItemTooltipDetour(AddonItemDetail* addonItemDetail, NumberArrayData* numberArrayData, StringArrayData* stringArrayData);
}
