using System;
using System.Collections.Generic;
using System.Linq;
using CriticalCommonLib.Enums;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Microsoft.Extensions.Logging;
using CSInventoryType = FFXIVClientStructs.FFXIV.Client.Game.InventoryType;

namespace CriticalCommonLib.Services
{
    public class TooltipService : ITooltipService
    {
        private readonly ILogger<TooltipService> _logger;
        private List<TooltipTweak> _tooltipTweaks = new();

        public void AddTooltipTweak(TooltipTweak tooltipTweak)
        {
            _tooltipTweaks.Add(tooltipTweak);
        }

        public abstract class TooltipTweak
        {
            private readonly ILogger _logger;

            public TooltipTweak(ILogger logger)
            {
                _logger = logger;
            }
            public abstract bool IsEnabled { get; }

            public DalamudLinkPayload? IdentifierPayload { get; set; }

            protected unsafe bool GetTooltipVisibility(ItemTooltipFieldVisibility tooltipField)
            {
                var flags = (ItemTooltipFieldVisibility)RaptureAtkModule.Instance()->AtkArrayDataHolder.GetNumberArrayData(30)->IntArray[5];
                return flags.HasFlag(tooltipField);
            }

            protected unsafe bool GetTooltipVisibility(ItemTooltipField tooltipField)
            {
                return RaptureAtkModule.Instance()->AtkArrayDataHolder.GetNumberArrayData(30)->IntArray[(int)tooltipField] == 0;
            }

            public virtual unsafe void OnGenerateItemTooltip(NumberArrayData* numberArrayData,
                StringArrayData* stringArrayData)
            {
            }

            protected unsafe SeString?
                GetTooltipString(StringArrayData* stringArrayData, ItemTooltipField field) =>
                GetTooltipString(stringArrayData, (int)field);

            protected unsafe SeString? GetTooltipString(StringArrayData* stringArrayData, int field) {
                try {
                    if (stringArrayData->AtkArrayData.Size <= field)
                        throw new IndexOutOfRangeException($"Attempted to get Index#{field} ({field}) but size is only {stringArrayData->AtkArrayData.Size}");

                    var stringAddress = new IntPtr(stringArrayData->StringArray[field]);
                    return stringAddress == IntPtr.Zero ? null : MemoryHelper.ReadSeStringNullTerminated(stringAddress);
                } catch (Exception ex) {
                    _logger.LogError(ex, "Failed to get tooltip string");
                    return new SeString();
                }
            }

            protected unsafe void SetTooltipString(StringArrayData* stringArrayData, ItemTooltipField field, SeString seString) {
                try {
                    seString ??= new SeString();

                    var bytes = seString.Encode().ToList();
                    bytes.Add(0);
                    stringArrayData->SetValue((int)field, bytes.ToArray(), false, true, false);
                } catch (Exception ex) {
                    _logger.LogError(ex, "Failed to set tooltip string");
                }
            }
            protected InventoryItem Item => HoveredItem;
        }

        // The hook delegates have to match the game's documented signature exactly (Dalamud verifies them against
        // the FFXIVClientStructs metadata when the hook is created), so reuse the client structs delegate types
        // instead of declaring our own approximation.
        [Signature("E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? 48 89 9C 24 ?? ?? ?? ?? 4C 89 A4 24", DetourName = nameof(ItemHoveredDetour), UseFlags = SignatureUseFlags.Hook)]
        private Hook<AgentItemDetail.Delegates.OnItemHovered>? _itemHoveredHook = null;

        [Signature("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 48 8B 42 ?? 4C 8B EA", DetourName = nameof(GenerateItemTooltipDetour), UseFlags = SignatureUseFlags.Hook)]
        private Hook<AddonItemDetail.Delegates.GenerateItemTooltip>? _generateItemTooltipHook = null;

        public TooltipService(IGameInteropProvider gameInteropProvider, ILogger<TooltipService> logger, IFramework framework)
        {
            _logger = logger;
            framework.RunOnFrameworkThread(() =>
            {
                gameInteropProvider.InitializeFromAttributes(this);
                _generateItemTooltipHook?.Enable();
                _itemHoveredHook?.Enable();
            });

            _logger.LogDebug("Creating {type} ({this})", GetType().Name, this);

        }


        public static InventoryItem HoveredItem { get; private set; }

        public unsafe bool ItemHoveredDetour(AgentItemDetail* agentItemDetail, InventoryItem** inventoryItem, CSInventoryType* inventoryType, short* a4, uint a5, uint a6, InventoryItem* a7) {
            var returnValue = _itemHoveredHook!.Original(agentItemDetail, inventoryItem, inventoryType, a4, a5, a6, a7);
            HoveredItem = *a7;
            return returnValue;
        }

        public void Disable() {
            _itemHoveredHook?.Disable();
            _generateItemTooltipHook?.Disable();
        }

        public void Dispose() {
            _logger.LogDebug("Disposing {type} ({this})", GetType().Name, this);
            _itemHoveredHook?.Dispose();
            _generateItemTooltipHook?.Dispose();
        }

        public unsafe void GenerateItemTooltipDetour(AddonItemDetail* addonItemDetail, NumberArrayData* numberArrayData, StringArrayData* stringArrayData)
        {
            try
            {
                foreach (var t in _tooltipTweaks)
                {
                    try
                    {
                        if (t.IsEnabled)
                        {
                            t.OnGenerateItemTooltip(numberArrayData, stringArrayData);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception while modifying item tooltip");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while modifying item tooltip");
            }
            _generateItemTooltipHook!.Original(addonItemDetail, numberArrayData, stringArrayData);
        }

        public enum ItemTooltipField : byte {
            ItemName,
            GlamourName,
            ItemUiCategory,
            ItemDescription = 13,
            Effects = 16,
            ClassJobCategory = 22,
            Levels = 23,
            DurabilityPercent = 28,
            SpiritbondPercent = 30,
            ExtractableProjectableDesynthesizable = 35,
            Param0 = 37,
            Param1 = 38,
            Param2 = 39,
            Param3 = 40,
            Param4 = 41,
            Param5 = 42,
            ShopSellingPrice = 63,
            ControlsDisplay = 64,
        }
    }
}