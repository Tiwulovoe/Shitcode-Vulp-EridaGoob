// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client.GameTicking.Managers;
using Content.Client.Lobby;
using Content.Shared.Audio.Events;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared._Erida.CCVar;
using Robust.Client;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Audio;

// Part of ContentAudioSystem that is responsible for lobby music playing/stopping and round-end sound-effect.
public sealed partial class ContentAudioSystem
{
    [Dependency] private readonly IBaseClient _client = default!;
    [Dependency] private readonly ClientGameTicker _gameTicker = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private readonly AudioParams _lobbySoundtrackParams = new(-5f, 1, 0, 0, 0, false, 0f);
    private readonly AudioParams _roundEndSoundEffectParams = new(-5f, 1, 0, 0, 0, false, 0f);

    /// <summary>
    /// EntityUid of lobby restart sound component.
    /// </summary>
    private EntityUid? _lobbyRoundRestartAudioStream;

    /// <summary>
    /// Shuffled list of soundtrack file-names.
    /// </summary>
    private string[]? _lobbyPlaylist;

    /// <summary>
    /// Short info about lobby soundtrack currently playing. Is null if soundtrack is not playing.
    /// </summary>
    private LobbySoundtrackInfo? _lobbySoundtrackInfo;

    private Action<LobbySoundtrackChangedEvent>? _lobbySoundtrackChanged;

    private bool _lobbyMusicPaused;
    private TimeSpan? _lobbyMusicPausedAt;
    private TimeSpan? _lobbyMusicStartCooldown;

    /// <summary>
    /// Event fired whenever the lobby player state changes: track, pause state, volume or enabled flag.
    /// </summary>
    public event Action? LobbyMusicStateChanged;

    /// <summary>
    /// True if the current lobby track is paused by the player.
    /// </summary>
    public bool LobbyMusicPaused => _lobbyMusicPaused;

    /// <summary>
    /// Whether lobby music is enabled in the options.
    /// </summary>
    public bool LobbyMusicEnabled => _configManager.GetCVar(CCVars.LobbyMusicEnabled);

    /// <summary>
    /// Whether the server sent a non-empty lobby playlist.
    /// </summary>
    public bool HasLobbyPlaylist => _lobbyPlaylist is { Length: > 0 };

    /// <summary>
    /// Number of tracks in the server lobby playlist.
    /// </summary>
    public int LobbyPlaylistLength => _lobbyPlaylist?.Length ?? 0;

    /// <summary>
    /// File name of the track that is currently playing, or null.
    /// </summary>
    public string? CurrentLobbySoundtrack => _lobbySoundtrackInfo?.Filename;

    /// <summary>
    /// Lobby music gain in the 0..1 range.
    /// </summary>
    public float GetLobbyMusicVolume()
    {
        return _configManager.GetCVar(CCVars.LobbyMusicVolume);
    }

    /// <summary>
    /// Sets the lobby music gain and persists it in the archived client CVar.
    /// </summary>
    public void SetLobbyMusicVolume(float volume)
    {
        _configManager.SetCVar(CCVars.LobbyMusicVolume, Math.Clamp(volume, 0f, 1f));
        LobbyMusicStateChanged?.Invoke();
    }

    /// <summary>
    /// Enables or disables lobby music. Starting and stopping is handled by the CVar subscription.
    /// </summary>
    public void SetLobbyMusicEnabled(bool enabled)
    {
        _configManager.SetCVar(CCVars.LobbyMusicEnabled, enabled);
        LobbyMusicStateChanged?.Invoke();
    }

    /// <summary>
    /// Pauses or resumes the current lobby track without restarting it.
    /// </summary>
    public void ToggleLobbyMusicPaused()
    {
        if (_lobbySoundtrackInfo == null)
            return;

        if (_lobbyMusicPaused)
            ResumeLobbyMusic();
        else
            PauseLobbyMusic();

        LobbyMusicStateChanged?.Invoke();
    }

    /// <summary>
    /// Switches to the next track of the server playlist.
    /// </summary>
    public void PlayNextLobbyTrack()
    {
        SwitchLobbyTrack(1);
    }

    /// <summary>
    /// Switches to the previous track of the server playlist.
    /// </summary>
    public void PlayPreviousLobbyTrack()
    {
        SwitchLobbyTrack(-1);
    }

    private void PauseLobbyMusic()
    {
        if (_lobbySoundtrackInfo == null || _lobbyMusicPaused)
            return;

        _audio.SetState(_lobbySoundtrackInfo.MusicStreamEntityUid, AudioState.Paused, force: true);
        _lobbyMusicPausedAt = _timing.CurTime;
        _lobbyMusicPaused = true;
    }

    private void ResumeLobbyMusic()
    {
        if (_lobbySoundtrackInfo == null || !_lobbyMusicPaused)
            return;

        _audio.SetState(_lobbySoundtrackInfo.MusicStreamEntityUid, AudioState.Playing, force: true);

        if (_lobbyMusicPausedAt != null)
        {
            var pausedFor = _timing.CurTime - _lobbyMusicPausedAt.Value;
            _lobbySoundtrackInfo = _lobbySoundtrackInfo with
            {
                NextTrackOn = _lobbySoundtrackInfo.NextTrackOn + pausedFor,
            };
        }

        _lobbyMusicPausedAt = null;
        _lobbyMusicPaused = false;
    }

    private void SwitchLobbyTrack(int direction)
    {
        if (_lobbySoundtrackInfo == null || _lobbyPlaylist is not { Length: > 1 })
            return;

        var current = Array.IndexOf(_lobbyPlaylist, _lobbySoundtrackInfo.Filename);
        if (current < 0)
            current = 0;

        var next = (current + direction + _lobbyPlaylist.Length) % _lobbyPlaylist.Length;
        var filename = _lobbyPlaylist[next];

        _audio.Stop(_lobbySoundtrackInfo.MusicStreamEntityUid);
        _lobbySoundtrackInfo = null;
        _lobbyMusicPaused = false;
        _lobbyMusicPausedAt = null;

        PlaySoundtrack(filename);
    }

    /// <summary>
    /// Enables lobby music once for players whose archived settings were saved before the
    /// in-lobby player existed, and restores a zero (muted) volume to a sensible default.
    /// Later explicit changes to the checkbox/volume are respected.
    /// </summary>
    private void EnsureLobbyMusicDefaults()
    {
        const int currentVersion = 2;

        if (_configManager.GetCVar(ECCVars.LobbyMusicDefaultsVersion) >= currentVersion)
            return;

        _configManager.SetCVar(CCVars.LobbyMusicEnabled, true);

        if (_configManager.GetCVar(CCVars.LobbyMusicVolume) <= 0.001f)
            _configManager.SetCVar(CCVars.LobbyMusicVolume, 0.5f);

        _configManager.SetCVar(ECCVars.LobbyMusicDefaultsVersion, currentVersion);

        Log.Info($"lobby-music: defaults migrated to enabled={_configManager.GetCVar(CCVars.LobbyMusicEnabled)} volume={_configManager.GetCVar(CCVars.LobbyMusicVolume)}");
    }

    /// <summary>
    /// Starts lobby music when it is enabled and a playlist is known but no track is playing.
    /// Safe to call every frame; it is a no-op once a track is actually playing. Does not override
    /// an explicit pause made by the player.
    /// </summary>
    public void EnsureLobbyMusicPlaying()
    {
        if (!LobbyMusicEnabled)
            return;

        if (_lobbyMusicPaused)
            return;

        if (_lobbySoundtrackInfo == null)
        {
            // Retry starting at most once a second so a failed resource load does not spam every frame.
            if (_lobbyMusicStartCooldown != null && _timing.CurTime < _lobbyMusicStartCooldown)
                return;

            _lobbyMusicStartCooldown = _timing.CurTime + TimeSpan.FromSeconds(1);
            StartLobbyMusic();
            return;
        }

        _lobbyMusicStartCooldown = null;

        // Make sure the stream is actually in the playing state rather than paused at the audio level.
        var entity = _lobbySoundtrackInfo.MusicStreamEntityUid;
        if (TryComp<AudioComponent>(entity, out var audio) && audio.State != AudioState.Playing)
            _audio.SetState(entity, AudioState.Playing, force: true);
    }

    /// <summary>
    /// Event for subscription on lobby soundtrack changes.
    /// </summary>
    public event Action<LobbySoundtrackChangedEvent>? LobbySoundtrackChanged
    {
        add
        {
            if (value != null)
            {
                if (_lobbySoundtrackInfo != null)
                {
                    value(new LobbySoundtrackChangedEvent(_lobbySoundtrackInfo.Filename));
                }

                _lobbySoundtrackChanged += value;
            }
        }
        remove => _lobbySoundtrackChanged -= value;
    }

    /// <summary>
    /// Initializes subscriptions that are related to lobby music.
    /// </summary>
    private void InitializeLobbyMusic()
    {
        Subs.CVar(_configManager, CCVars.LobbyMusicEnabled, LobbyMusicCVarChanged);
        Subs.CVar(_configManager, CCVars.LobbyMusicVolume, LobbyMusicVolumeCVarChanged);

        EnsureLobbyMusicDefaults();

        _state.OnStateChanged += StateManagerOnStateChanged;

        _client.PlayerLeaveServer += OnLeave;

        SubscribeNetworkEvent<LobbyMusicStopEvent>(OnLobbySongStopped);
        SubscribeNetworkEvent<LobbyPlaylistChangedEvent>(OnLobbySongChanged);
    }

    private void OnLobbySongStopped(LobbyMusicStopEvent ev)
    {
        EndLobbyMusic();
    }

    private void StateManagerOnStateChanged(StateChangedEventArgs args)
    {
        Log.Info($"lobby-music: state changed to {args.NewState.GetType().Name}");

        switch (args.NewState)
        {
            case LobbyState:
                StartLobbyMusic();
                break;
            default:
                EndLobbyMusic();
                break;
        }
    }

    private void OnLeave(object? sender, PlayerEventArgs args)
    {
        EndLobbyMusic();
    }

    private void LobbyMusicVolumeCVarChanged(float volume)
    {
        if (_lobbySoundtrackInfo != null)
        {
            _audio.SetVolume(
                _lobbySoundtrackInfo.MusicStreamEntityUid,
                _lobbySoundtrackParams.Volume + SharedAudioSystem.GainToVolume(_configManager.GetCVar(CCVars.LobbyMusicVolume))
            );
        }

        LobbyMusicStateChanged?.Invoke();
    }

    private void LobbyMusicCVarChanged(bool musicEnabled)
    {
        if (musicEnabled && _state.CurrentState is LobbyState)
        {
            StartLobbyMusic();
        }
        else
        {
            EndLobbyMusic();
        }

        LobbyMusicStateChanged?.Invoke();
    }

    private void OnLobbySongChanged(LobbyPlaylistChangedEvent playlistChangedEvent)
    {
        var playlist = playlistChangedEvent.Playlist;
        //playlist is already playing, no need to restart it
        if (_lobbySoundtrackInfo != null
            && _lobbyPlaylist != null
            && _lobbyPlaylist.SequenceEqual(playlist)
           )
        {
            return;
        }

        EndLobbyMusic();
        StartLobbyMusic(playlistChangedEvent.Playlist);
    }

    /// <summary>
    /// Re-starts playing lobby music from playlist, last sent from server. if there is currently none - does nothing.
    /// </summary>
    private void StartLobbyMusic()
    {
        if (_lobbyPlaylist == null || _lobbyPlaylist.Length == 0)
        {
            return;
        }

        StartLobbyMusic(_lobbyPlaylist);
    }

    /// <summary>
    /// Starts playing lobby music from playlist. If playlist is empty, or lobby music setting is turned off - does nothing.
    /// </summary>
    /// <param name="playlist">Array of soundtrack filenames for lobby playlist.</param>
    private void StartLobbyMusic(string[] playlist)
    {
        // Store the playlist even while music is disabled, so the lobby player can start it later.
        _lobbyPlaylist = playlist;

        Log.Info($"lobby-music: StartLobbyMusic(playlist) length={playlist.Length} enabled={_configManager.GetCVar(CCVars.LobbyMusicEnabled)} volume={_configManager.GetCVar(CCVars.LobbyMusicVolume)} soundtrackInfo={(_lobbySoundtrackInfo != null)}");

        if (_lobbySoundtrackInfo != null || !_configManager.GetCVar(CCVars.LobbyMusicEnabled))
            return;

        if (_lobbyPlaylist.Length == 0)
            return;

        _lobbyMusicPaused = false;
        _lobbyMusicPausedAt = null;
        PlaySoundtrack(playlist[0]);
    }

    private void PlaySoundtrack(string soundtrackFilename)
    {
        if (!_resourceCache.TryGetResource(new ResPath(soundtrackFilename), out AudioResource? audio))
        {
            Log.Warning($"lobby-music: failed to load resource '{soundtrackFilename}'");
            return;
        }

        var playResult = _audio.PlayGlobal(
            soundtrackFilename,
            Filter.Local(),
            false,
            _lobbySoundtrackParams.WithVolume(_lobbySoundtrackParams.Volume + SharedAudioSystem.GainToVolume(_configManager.GetCVar(CCVars.LobbyMusicVolume)))
        );
        if (playResult == null)
        {
            _sawmill.Warning(
                $"Tried to play lobby soundtrack '{{Filename}}' using {nameof(SharedAudioSystem)}.{nameof(SharedAudioSystem.PlayGlobal)} but it returned default value of EntityUid!",
                soundtrackFilename);
            return;
        }

        var nextTrackOn = _timing.CurTime + audio.AudioStream.Length;
        _lobbySoundtrackInfo = new LobbySoundtrackInfo(soundtrackFilename, nextTrackOn, playResult.Value.Entity);
        _lobbyMusicPaused = false;
        _lobbyMusicPausedAt = null;

        Log.Info($"lobby-music: started '{soundtrackFilename}' volume={_lobbySoundtrackParams.Volume + SharedAudioSystem.GainToVolume(_configManager.GetCVar(CCVars.LobbyMusicVolume))}");

        var lobbySongChangedEvent = new LobbySoundtrackChangedEvent(soundtrackFilename);
        _lobbySoundtrackChanged?.Invoke(lobbySongChangedEvent);
        LobbyMusicStateChanged?.Invoke();
    }

    private void EndLobbyMusic()
    {
        if (_lobbySoundtrackInfo == null)
        {
            LobbyMusicStateChanged?.Invoke();
            return;
        }

        _audio.Stop(_lobbySoundtrackInfo.MusicStreamEntityUid);
        _lobbySoundtrackInfo = null;
        _lobbyMusicPaused = false;
        _lobbyMusicPausedAt = null;
        var lobbySongChangedEvent = new LobbySoundtrackChangedEvent();
        _lobbySoundtrackChanged?.Invoke(lobbySongChangedEvent);
        LobbyMusicStateChanged?.Invoke();
    }

    private void PlayRestartSound(RoundRestartCleanupEvent ev)
    {
        if (!_configManager.GetCVar(CCVars.RestartSoundsEnabled))
            return;

        var file = _gameTicker.RestartSound;
        if (ResolvedSoundSpecifier.IsNullOrEmpty(file))
        {
            return;
        }

        _lobbyRoundRestartAudioStream = _audio.PlayGlobal(
            file,
            Filter.Local(),
            false,
            _roundEndSoundEffectParams.WithVolume(_roundEndSoundEffectParams.Volume + SharedAudioSystem.GainToVolume(_configManager.GetCVar(CCVars.LobbyMusicVolume)))
        )?.Entity;
    }

    private void ShutdownLobbyMusic()
    {
        _state.OnStateChanged -= StateManagerOnStateChanged;

        _client.PlayerLeaveServer -= OnLeave;

        EndLobbyMusic();
    }

    private void UpdateLobbyMusic()
    {
        if (
            _lobbySoundtrackInfo != null
            && !_lobbyMusicPaused
            && _timing.CurTime >= _lobbySoundtrackInfo.NextTrackOn
            && _lobbyPlaylist?.Length > 0
            )
        {
            var nextSoundtrackFilename = GetNextSoundtrackFromPlaylist(_lobbySoundtrackInfo.Filename, _lobbyPlaylist);
            PlaySoundtrack(nextSoundtrackFilename);
        }
    }

    private static string GetNextSoundtrackFromPlaylist(string currentSoundtrackFilename, string[] playlist)
    {
        var indexOfCurrent = Array.IndexOf(playlist, currentSoundtrackFilename);
        var nextTrackIndex = indexOfCurrent + 1;
        if (nextTrackIndex > playlist.Length - 1)
        {
            nextTrackIndex = 0;
        }

        return playlist[nextTrackIndex];
    }

    /// <summary> Container for lobby soundtrack information. </summary>
    /// <param name="Filename">Soundtrack filename.</param>
    /// <param name="NextTrackOn">Time (based on <see cref="IGameTiming.CurTime"/>) when this track is going to finish playing and next track have to be started.</param>
    /// <param name="MusicStreamEntityUid">
    /// EntityUid of launched soundtrack (from <see cref="SharedAudioSystem.PlayGlobal(string,Robust.Shared.Player.Filter,bool,System.Nullable{Robust.Shared.Audio.AudioParams})"/>).
    /// </param>
    private sealed record LobbySoundtrackInfo(string Filename, TimeSpan NextTrackOn, EntityUid MusicStreamEntityUid);
}

/// <summary>
/// Event of changing lobby soundtrack (or stopping lobby music - will pass null for <paramref name="SoundtrackFilename"/> in that case).
/// Is used by <see cref="ContentAudioSystem.LobbySoundtrackChanged"/> and <see cref="LobbyState.UpdateLobbySoundtrackInfo"/>.
/// </summary>
/// <param name="SoundtrackFilename">Filename of newly set soundtrack, or null if soundtrack playback is stopped.</param>
public sealed record LobbySoundtrackChangedEvent(string? SoundtrackFilename = null);