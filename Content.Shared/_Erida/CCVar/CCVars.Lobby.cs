// SPDX-FileCopyrightText: 2026 Lytheriia
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared._Erida.CCVar;

public sealed partial class ECCVars
{
    /// <summary>
    ///     Whether the lobby shows the background picked by the server for the round.
    /// </summary>
    public static readonly CVarDef<bool> LobbyBackgroundRandom =
        CVarDef.Create("erida.ui.lobby_background_random", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Lobby background prototype shown while <see cref="LobbyBackgroundRandom"/> is off.
    /// </summary>
    public static readonly CVarDef<string> LobbyBackground =
        CVarDef.Create("erida.ui.lobby_background", "LuaMSky2", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Lobby layout preset: sidebar, screen or custom.
    /// </summary>
    public static readonly CVarDef<string> LobbyLayoutPreset =
        CVarDef.Create("erida.ui.lobby_layout_preset", "sidebar", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Per-element lobby layout for the custom preset, stored as a compact key=value list.
    /// </summary>
    public static readonly CVarDef<string> LobbyLayout =
        CVarDef.Create("erida.ui.lobby_layout", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Layout schema version. Bumped when defaults/available placements change so old
    ///     layouts do not keep obsolete panels or screen placements.
    /// </summary>
    public static readonly CVarDef<int> LobbyLayoutVersion =
        CVarDef.Create("erida.ui.lobby_layout_version", 0, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Lobby music defaults version. Used to enable lobby music once for players whose
    ///     archived settings predate the in-lobby music player.
    /// </summary>
    public static readonly CVarDef<int> LobbyMusicDefaultsVersion =
        CVarDef.Create("erida.ui.lobby_music_defaults_version", 0, CVar.CLIENTONLY | CVar.ARCHIVE);
}
