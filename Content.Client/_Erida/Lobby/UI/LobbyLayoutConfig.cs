// SPDX-FileCopyrightText: 2026 EridaGoob
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Erida.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client._Erida.Lobby.UI;

/// <summary>
/// Elements of the lobby layout. Only elements with <see cref="LobbyElementInfo.CanScreen"/>
/// may leave the sidebar.
/// </summary>
public enum LobbyElement : byte
{
    QuickActions,
    Links,
    ServerInfo,
    CharacterPreview,
    Chat,
    MusicPlayer,
    Balance,
    Playtime,
}

/// <summary>
/// Where a single lobby element is shown.
/// </summary>
public enum LobbyPlacement : byte
{
    Sidebar,
    OnScreen,
}

/// <summary>
/// Layout preset. Any per-element change switches the preset to <see cref="Custom"/>.
/// </summary>
public enum LobbyLayoutPreset : byte
{
    Custom,
    Sidebar,
    OnScreen,
}

/// <summary>
/// Default corner the floating panel docks to on the screen.
/// </summary>
public enum LobbyScreenDock : byte
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public sealed record LobbyElementInfo(
    LobbyElement Element,
    string Key,
    string NameLoc,
    LobbyPlacement DefaultPlacement,
    bool CanScreen,
    LobbyScreenDock Dock);

/// <summary>
/// Reads and writes the player's lobby layout. Everything is stored in archived client CVars,
/// so the choice survives reconnects without touching the server.
/// </summary>
public static class LobbyLayoutConfig
{
    /// <summary>
    /// Bump when the available placements or default layout change.
    /// </summary>
    public const int CurrentVersion = 4;

    private const string PresetSidebar = "sidebar";
    private const string PresetScreen = "screen";
    private const string PresetCustom = "custom";

    private const string PlacementSidebar = "sidebar";
    private const string PlacementScreen = "screen";

    public static readonly LobbyElementInfo[] Elements =
    [
        new(LobbyElement.QuickActions, "quick_actions", "lobby-layout-element-quick-actions",
            LobbyPlacement.Sidebar, false, LobbyScreenDock.TopLeft),
        new(LobbyElement.Links, "links", "lobby-layout-element-links",
            LobbyPlacement.Sidebar, false, LobbyScreenDock.TopLeft),
        new(LobbyElement.Balance, "balance", "lobby-layout-element-balance",
            LobbyPlacement.Sidebar, false, LobbyScreenDock.TopLeft),
        new(LobbyElement.ServerInfo, "server_info", "lobby-layout-element-server-info",
            LobbyPlacement.Sidebar, true, LobbyScreenDock.TopLeft),
        new(LobbyElement.CharacterPreview, "character", "lobby-layout-element-character",
            LobbyPlacement.Sidebar, true, LobbyScreenDock.TopRight),
        new(LobbyElement.Chat, "chat", "lobby-layout-element-chat",
            LobbyPlacement.Sidebar, true, LobbyScreenDock.BottomRight),
        new(LobbyElement.Playtime, "playtime", "lobby-layout-element-playtime",
            LobbyPlacement.Sidebar, false, LobbyScreenDock.BottomLeft),
        new(LobbyElement.MusicPlayer, "music", "lobby-layout-element-music",
            LobbyPlacement.Sidebar, true, LobbyScreenDock.BottomLeft),
    ];

    public static LobbyLayoutPreset GetPreset(IConfigurationManager cfg)
    {
        return cfg.GetCVar(ECCVars.LobbyLayoutPreset) switch
        {
            PresetSidebar => LobbyLayoutPreset.Sidebar,
            PresetScreen => LobbyLayoutPreset.OnScreen,
            _ => LobbyLayoutPreset.Custom,
        };
    }

    public static void SetPreset(IConfigurationManager cfg, LobbyLayoutPreset preset)
    {
        var value = preset switch
        {
            LobbyLayoutPreset.Sidebar => PresetSidebar,
            LobbyLayoutPreset.OnScreen => PresetScreen,
            _ => PresetCustom,
        };

        cfg.SetCVar(ECCVars.LobbyLayoutPreset, value);
    }

    /// <summary>
    /// Switches to the custom preset and restores the balanced per-element defaults.
    /// </summary>
    public static void SetDefaultCustomLayout(IConfigurationManager cfg)
    {
        var result = new Dictionary<LobbyElement, LobbyPlacement>();

        foreach (var element in Elements)
        {
            result[element.Element] = element.DefaultPlacement;
        }

        cfg.SetCVar(ECCVars.LobbyLayout, Serialize(result));
        cfg.SetCVar(ECCVars.LobbyLayoutPreset, PresetCustom);
    }

    /// <summary>
    /// Migrates an old saved lobby layout to the current defaults once.
    /// </summary>
    public static void EnsureCurrentVersion(IConfigurationManager cfg)
    {
        if (cfg.GetCVar(ECCVars.LobbyLayoutVersion) >= CurrentVersion)
            return;

        SetDefaultCustomLayout(cfg);
        SetPreset(cfg, LobbyLayoutPreset.Sidebar);
        cfg.SetCVar(ECCVars.LobbyLayoutVersion, CurrentVersion);
    }

    public static LobbyPlacement GetPlacement(IConfigurationManager cfg, LobbyElement element)
    {
        return GetAllPlacements(cfg).TryGetValue(element, out var placement)
            ? placement
            : GetDefaultPlacement(element);
    }

    public static IReadOnlyDictionary<LobbyElement, LobbyPlacement> GetAllPlacements(IConfigurationManager cfg)
    {
        var preset = GetPreset(cfg);
        var result = new Dictionary<LobbyElement, LobbyPlacement>();

        if (preset != LobbyLayoutPreset.Custom)
        {
            foreach (var element in Elements)
            {
                var placement = preset == LobbyLayoutPreset.Sidebar
                    ? LobbyPlacement.Sidebar
                    : element.CanScreen ? LobbyPlacement.OnScreen : LobbyPlacement.Sidebar;

                result[element.Element] = placement;
            }

            return result;
        }

        ParseInto(cfg.GetCVar(ECCVars.LobbyLayout), result);

        foreach (var element in Elements)
        {
            if (!result.TryGetValue(element.Element, out var placement))
                placement = element.DefaultPlacement;

            // Only chat, character, server info and music may be moved to the screen.
            if (!element.CanScreen && placement == LobbyPlacement.OnScreen)
                placement = LobbyPlacement.Sidebar;

            result[element.Element] = placement;
        }

        return result;
    }

    public static void SetPlacement(IConfigurationManager cfg, LobbyElement element, LobbyPlacement placement)
    {
        if (!CanScreen(element) && placement == LobbyPlacement.OnScreen)
            placement = LobbyPlacement.Sidebar;

        var result = new Dictionary<LobbyElement, LobbyPlacement>(GetAllPlacements(cfg));
        result[element] = placement;

        cfg.SetCVar(ECCVars.LobbyLayout, Serialize(result));
        cfg.SetCVar(ECCVars.LobbyLayoutPreset, PresetCustom);
    }

    public static LobbyPlacement GetDefaultPlacement(LobbyElement element)
    {
        foreach (var info in Elements)
        {
            if (info.Element == element)
                return info.DefaultPlacement;
        }

        return LobbyPlacement.Sidebar;
    }

    public static bool CanScreen(LobbyElement element)
    {
        foreach (var info in Elements)
        {
            if (info.Element == element)
                return info.CanScreen;
        }

        return false;
    }

    private static void ParseInto(string raw, Dictionary<LobbyElement, LobbyPlacement> target)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        foreach (var part in raw.Split(','))
        {
            var split = part.Split('=', 2);
            if (split.Length != 2)
                continue;

            var key = split[0].Trim();
            if (!TryGetElement(key, out var element))
                continue;

            if (TryParsePlacement(split[1].Trim(), out var placement))
                target[element] = placement;
        }
    }

    private static string Serialize(IReadOnlyDictionary<LobbyElement, LobbyPlacement> placements)
    {
        var parts = new List<string>(Elements.Length);

        foreach (var info in Elements)
        {
            if (!placements.TryGetValue(info.Element, out var placement))
                placement = info.DefaultPlacement;

            parts.Add($"{info.Key}={PlacementToKey(placement)}");
        }

        return string.Join(",", parts);
    }

    private static bool TryGetElement(string key, out LobbyElement element)
    {
        foreach (var info in Elements)
        {
            if (!string.Equals(info.Key, key, StringComparison.OrdinalIgnoreCase))
                continue;

            element = info.Element;
            return true;
        }

        element = default;
        return false;
    }

    private static string PlacementToKey(LobbyPlacement placement)
    {
        return placement switch
        {
            LobbyPlacement.Sidebar => PlacementSidebar,
            _ => PlacementScreen,
        };
    }

    private static bool TryParsePlacement(string value, out LobbyPlacement placement)
    {
        switch (value.ToLowerInvariant())
        {
            case PlacementSidebar:
                placement = LobbyPlacement.Sidebar;
                return true;
            case PlacementScreen:
                placement = LobbyPlacement.OnScreen;
                return true;
            default:
                placement = default;
                return false;
        }
    }
}
